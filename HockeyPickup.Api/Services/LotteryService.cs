using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Services;

public enum LotteryDrawOutcome
{
    Completed,
    NoOp,
    Reschedule
}

public interface ILotteryService
{
    Task<ServiceResult<BuySellStatusResponse>> EnterAsync(string userId, int sessionId);
    Task<ServiceResult<bool>> WithdrawAsync(string userId, int sessionId);
    Task<LotteryDrawOutcome> HandleDrawMessageAsync(LotteryDrawMessage message, CancellationToken cancellationToken = default);
    Task<ServiceResult<int>> ExecuteDueAsync();
    Task EnqueueDrawMessagesAsync(SessionDetailedResponse session);
}

public class LotteryService : ILotteryService
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILotteryRepository _lotteryRepository;
    private readonly IBuySellService _buySellService;
    private readonly IServiceBus _serviceBus;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LotteryService> _logger;
    private readonly IRandomShuffler _shuffler;
    private readonly IUserRepository _userRepository;

    private static readonly TimeSpan StuckDrawingThreshold = TimeSpan.FromMinutes(10);

    public LotteryService(
        UserManager<AspNetUser> userManager,
        ISessionRepository sessionRepository,
        ILotteryRepository lotteryRepository,
        IBuySellService buySellService,
        IServiceBus serviceBus,
        IConfiguration configuration,
        ILogger<LotteryService> logger,
        IRandomShuffler shuffler,
        IUserRepository userRepository)
    {
        _userManager = userManager;
        _sessionRepository = sessionRepository;
        _lotteryRepository = lotteryRepository;
        _buySellService = buySellService;
        _serviceBus = serviceBus;
        _configuration = configuration;
        _logger = logger;
        _shuffler = shuffler;
        _userRepository = userRepository;
    }

    public async Task<ServiceResult<BuySellStatusResponse>> EnterAsync(string userId, int sessionId)
    {
        try
        {
            // Reuse CanBuyAsync so every roster/active/window check + the lottery gate is applied consistently.
            var canBuyResult = await _buySellService.CanBuyAsync(userId, sessionId);
            if (!canBuyResult.IsSuccess)
                return ServiceResult<BuySellStatusResponse>.CreateFailure(canBuyResult.Message);

            var status = canBuyResult.Data!;
            if (status.BuyActionState != BuyActionState.EnterLottery)
                return ServiceResult<BuySellStatusResponse>.CreateFailure(status.Reason);

            var buyer = await _userManager.FindByIdAsync(userId);
            if (buyer == null)
                return ServiceResult<BuySellStatusResponse>.CreateFailure("User not found");

            var session = await _sessionRepository.GetSessionAsync(sessionId);
            var lotteryClass = status.LotteryClass!.Value;
            var message = $"{buyer.FirstName} {buyer.LastName} entered the {lotteryClass} lottery";

            await _lotteryRepository.CreateOrReactivateEntrantAsync(sessionId, userId, lotteryClass, 1.0m, message);

            await SendLotteryEnteredServiceBusMessageAsync(session, buyer, lotteryClass);

            return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
            {
                IsAllowed = false,
                Reason = $"You are entered in the {lotteryClass} lottery",
                BuyActionState = BuyActionState.InLottery,
                LotteryClass = lotteryClass,
                TimeUntilDraw = status.TimeUntilDraw
            }, message);
        }
        catch (Exception ex)
        {
            var msg = $"Error entering lottery for user {userId} in session {sessionId}: {ex.GetRelevantMessage()}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellStatusResponse>.CreateFailure(msg);
        }
    }

    public async Task<ServiceResult<bool>> WithdrawAsync(string userId, int sessionId)
    {
        try
        {
            var buyer = await _userManager.FindByIdAsync(userId);
            if (buyer == null)
                return ServiceResult<bool>.CreateFailure("User not found");

            var entrant = await _lotteryRepository.GetEntrantAsync(sessionId, userId);
            if (entrant == null || entrant.Status != LotteryEntrantStatus.Entered)
                return ServiceResult<bool>.CreateFailure("You do not have an active lottery entry to withdraw");

            var message = $"{buyer.FirstName} {buyer.LastName} withdrew from the {entrant.LotteryClass} lottery";
            var withdrawn = await _lotteryRepository.WithdrawEntrantAsync(sessionId, userId, message);
            if (withdrawn == null)
                return ServiceResult<bool>.CreateFailure("You do not have an active lottery entry to withdraw");

            return ServiceResult<bool>.CreateSuccess(true, "You have withdrawn from the lottery");
        }
        catch (Exception ex)
        {
            var msg = $"Error withdrawing from lottery for user {userId} in session {sessionId}: {ex.GetRelevantMessage()}";
            _logger.LogError(ex, msg);
            return ServiceResult<bool>.CreateFailure(msg);
        }
    }

    public async Task<LotteryDrawOutcome> HandleDrawMessageAsync(LotteryDrawMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify-on-receive: a missing/disabled session or a moved draw time makes the message a harmless no-op.
            var session = await _sessionRepository.GetSessionAsync(message.SessionId);
            if (session == null || !session.LotteryEnabled)
            {
                _logger.LogInformation($"Lottery draw no-op: session {message.SessionId} missing or lottery disabled");
                return LotteryDrawOutcome.NoOp;
            }

            var draw = session.LotteryDrawFor(message.LotteryClass);
            if (draw.Ticks != message.ExpectedDrawDateTimePacific.Ticks)
            {
                _logger.LogInformation($"Lottery draw no-op: session {message.SessionId} {message.LotteryClass} draw time changed (expected {message.ExpectedDrawDateTimePacific:o}, recomputed {draw:o})");
                return LotteryDrawOutcome.NoOp;
            }

            var nowPacific = TimeZoneUtils.GetCurrentPacificTime();
            if (nowPacific < draw)
            {
                _logger.LogInformation($"Lottery draw early delivery: session {message.SessionId} {message.LotteryClass} draws at {draw:o}; rescheduling");
                return LotteryDrawOutcome.Reschedule;
            }

            // Atomic claim - 0 rows means nothing to draw (already drawn or zero entrants): silent no-op.
            var claimed = await _lotteryRepository.ClaimForDrawingAsync(message.SessionId, message.LotteryClass);
            if (claimed == 0)
            {
                _logger.LogInformation($"Lottery draw no-op: no entrants claimed for session {message.SessionId} {message.LotteryClass}");
                return LotteryDrawOutcome.NoOp;
            }

            var entrants = await _lotteryRepository.GetEntrantsAsync(message.SessionId, message.LotteryClass, LotteryEntrantStatus.Drawing);
            await ShuffleAndProcessAsync(session, message.LotteryClass, entrants);
            return LotteryDrawOutcome.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling lottery draw for session {message.SessionId} {message.LotteryClass}: {ex.GetRelevantMessage()}");
            // Complete the message to avoid a poison loop; the safety-net sweep recovers any half-finished draw.
            return LotteryDrawOutcome.NoOp;
        }
    }

    public async Task<ServiceResult<int>> ExecuteDueAsync()
    {
        try
        {
            var nowPacific = TimeZoneUtils.GetCurrentPacificTime();
            var drawsRun = 0;

            // (a) Lost messages: any (session, tier) past its draw time with Entered rows.
            var dueTiers = await _lotteryRepository.GetDueUndrawnTiersAsync(nowPacific);
            foreach (var (dueSessionId, dueClass) in dueTiers)
            {
                var session = await _sessionRepository.GetSessionAsync(dueSessionId);
                if (session == null || !session.LotteryEnabled)
                    continue;

                var outcome = await HandleDrawMessageAsync(new LotteryDrawMessage
                {
                    SessionId = dueSessionId,
                    LotteryClass = dueClass,
                    ExpectedDrawDateTimePacific = session.LotteryDrawFor(dueClass)
                });
                if (outcome == LotteryDrawOutcome.Completed)
                    drawsRun++;
            }

            // (b) Mid-draw crash recovery: rows stuck in Drawing for too long.
            var stuck = await _lotteryRepository.GetStuckDrawingAsync(DateTime.UtcNow - StuckDrawingThreshold);
            var stuckGroups = stuck
                .Select(e => (e.SessionId, e.LotteryClass))
                .Distinct()
                .ToList();
            foreach (var (stuckSessionId, stuckClass) in stuckGroups)
            {
                var session = await _sessionRepository.GetSessionAsync(stuckSessionId);
                if (session == null)
                    continue;

                var ordered = await _lotteryRepository.GetDrawingOrderedAsync(stuckSessionId, stuckClass);
                if (ordered.Count == 0)
                    continue;

                if (ordered.All(e => e.DrawOrder.HasValue))
                {
                    // DrawOrder already persisted - resume processing in the persisted order.
                    await ProcessInOrderAsync(session, stuckClass, ordered);
                }
                else
                {
                    // No persisted order - treat as a fresh draw over the claimed rows.
                    await ShuffleAndProcessAsync(session, stuckClass, ordered);
                }
                drawsRun++;
            }

            return ServiceResult<int>.CreateSuccess(drawsRun, $"Executed {drawsRun} due lottery draw(s)");
        }
        catch (Exception ex)
        {
            var msg = $"Error executing due lotteries: {ex.GetRelevantMessage()}";
            _logger.LogError(ex, msg);
            return ServiceResult<int>.CreateFailure(msg);
        }
    }

    public async Task EnqueueDrawMessagesAsync(SessionDetailedResponse session)
    {
        var queueName = _configuration["ServiceBusLotteryQueueName"];
        foreach (var lotteryClass in new[] { LotteryClass.PreferredPlus, LotteryClass.Preferred, LotteryClass.Standard })
        {
            var drawPacific = session.LotteryDrawFor(lotteryClass);
            var message = new LotteryDrawMessage
            {
                SessionId = session.SessionId,
                LotteryClass = lotteryClass,
                ExpectedDrawDateTimePacific = drawPacific
            };

            await _serviceBus.SendAsync(message,
                subject: "LotteryDraw",
                correlationId: $"{session.SessionId}:{lotteryClass}:{drawPacific.Ticks}",
                queueName: queueName,
                scheduledEnqueueTimeUtc: new DateTimeOffset(drawPacific.PacificToUtc(), TimeSpan.Zero));
        }
    }

    // Shuffle the claimed entrants, persist the 1-based draw order (committed before any buy), then process.
    private async Task ShuffleAndProcessAsync(SessionDetailedResponse session, LotteryClass lotteryClass, List<SessionLotteryEntrant> entrants)
    {
        _shuffler.Shuffle(entrants);

        var ordered = new List<(int LotteryEntrantId, int DrawOrder)>();
        for (var i = 0; i < entrants.Count; i++)
        {
            entrants[i].DrawOrder = i + 1;
            ordered.Add((entrants[i].LotteryEntrantId, i + 1));
        }

        await _lotteryRepository.PersistDrawOrderAsync(session.SessionId, lotteryClass, ordered, DateTime.UtcNow);

        await ProcessInOrderAsync(session, lotteryClass, entrants);
    }

    // Process entrants strictly in DrawOrder, buying on each one's behalf; one failure never aborts the draw.
    private async Task ProcessInOrderAsync(SessionDetailedResponse session, LotteryClass lotteryClass, List<SessionLotteryEntrant> entrants)
    {
        var ordered = entrants.OrderBy(e => e.DrawOrder).ToList();
        var drawnNames = new List<string>();
        var entrantEmails = new List<string>();

        foreach (var entrant in ordered)
        {
            var result = await _buySellService.ProcessBuyRequestAsync(entrant.UserId, new BuyRequest { SessionId = session.SessionId }, bypassLotteryGate: true);
            if (result.IsSuccess)
            {
                await _lotteryRepository.MarkDrawnAsync(entrant.LotteryEntrantId);
            }
            else
            {
                await _lotteryRepository.MarkFailedAsync(entrant.LotteryEntrantId, result.Message);
                _logger.LogWarning($"Lottery entrant {entrant.UserId} buy failed in {lotteryClass} draw for session {session.SessionId}: {result.Message}");
            }

            var fullName = $"{entrant.User?.FirstName} {entrant.User?.LastName}".Trim();
            drawnNames.Add(fullName);
            if (entrant.User != null && !string.IsNullOrEmpty(entrant.User.Email))
                entrantEmails.Add(entrant.User.Email);
        }

        var activity = $"Lottery Draw Results ({lotteryClass}): {string.Join(", ", drawnNames)}";
        await _sessionRepository.AddActivityAsync(session.SessionId, activity);

        await SendLotteryDrawCompletedServiceBusMessageAsync(session, lotteryClass, drawnNames, entrantEmails);
    }

    private async Task SendLotteryEnteredServiceBusMessageAsync(SessionDetailedResponse session, AspNetUser entrant, LotteryClass lotteryClass)
    {
        var baseUrl = _configuration["BaseUrl"];
        var sessionUrl = $"{baseUrl?.TrimEnd('/')}/session/{session.SessionId}";
        var drawPacific = session.LotteryDrawFor(lotteryClass);

        var users = await _userRepository.GetDetailedUsersAsync();
        var notificationEmails = users.Where(u => u.Active && u.NotificationPreference == NotificationPreference.All)
            .Select(u => u.Email).Where(email => !string.IsNullOrEmpty(email)).ToArray();

        var commsMessage = new ServiceBusCommsMessage
        {
            Metadata = new Dictionary<string, string>
            {
                { "Type", "LotteryEntered" },
                { "CommunicationEventId", Guid.NewGuid().ToString() }
            },
            CommunicationMethod = new Dictionary<string, string>
            {
                { "EntrantEmail", entrant.Email },
                { "EntrantNotificationPreference", entrant.NotificationPreference.ToString() }
            },
            RelatedEntities = new Dictionary<string, string>
            {
                { "EntrantUserId", entrant.Id },
                { "EntrantFirstName", entrant.FirstName ?? "" },
                { "EntrantLastName", entrant.LastName ?? "" },
                { "LotteryClass", lotteryClass.ToString() }
            },
            MessageData = new Dictionary<string, string>
            {
                { "SessionDate", session.SessionDate.ToString() },
                { "SessionUrl", sessionUrl },
                { "LotteryClass", lotteryClass.ToString() },
                { "DrawDateTime", drawPacific.ToString() }
            },
            NotificationEmails = notificationEmails!,
            NotificationDeviceIds = null
        };

        await _serviceBus.SendAsync(commsMessage, subject: "LotteryEntered", correlationId: Guid.NewGuid().ToString(), queueName: _configuration["ServiceBusCommsQueueName"]);
    }

    private async Task SendLotteryDrawCompletedServiceBusMessageAsync(SessionDetailedResponse session, LotteryClass lotteryClass, List<string> drawnNames, List<string> entrantEmails)
    {
        var baseUrl = _configuration["BaseUrl"];
        var sessionUrl = $"{baseUrl?.TrimEnd('/')}/session/{session.SessionId}";

        // Results go to every entrant of this draw plus everyone subscribed to all alerts (deduped).
        var users = await _userRepository.GetDetailedUsersAsync();
        var allAlertEmails = users.Where(u => u.Active && u.NotificationPreference == NotificationPreference.All)
            .Select(u => u.Email).Where(email => !string.IsNullOrEmpty(email));
        var recipients = entrantEmails.Concat(allAlertEmails).Distinct().ToList();

        var commsMessage = new ServiceBusCommsMessage
        {
            Metadata = new Dictionary<string, string>
            {
                { "Type", "LotteryDrawCompleted" },
                { "CommunicationEventId", Guid.NewGuid().ToString() }
            },
            CommunicationMethod = new Dictionary<string, string>
            {
                { "Method", "Email" }
            },
            RelatedEntities = new Dictionary<string, string>
            {
                { "LotteryClass", lotteryClass.ToString() }
            },
            MessageData = new Dictionary<string, string>
            {
                { "SessionDate", session.SessionDate.ToString() },
                { "SessionUrl", sessionUrl },
                { "LotteryClass", lotteryClass.ToString() },
                { "DrawOrderNames", string.Join("\n", drawnNames) }
            },
            NotificationEmails = recipients,
            NotificationDeviceIds = null
        };

        await _serviceBus.SendAsync(commsMessage, subject: "LotteryDrawCompleted", correlationId: Guid.NewGuid().ToString(), queueName: _configuration["ServiceBusCommsQueueName"]);
    }
}
