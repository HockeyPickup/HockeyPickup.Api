using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Services;

public interface IBuySellService
{
    Task<ServiceResult<BuySellResponse>> ProcessBuyRequestAsync(string userId, BuyRequest request);
    Task<ServiceResult<BuySellResponse>> ProcessSellRequestAsync(string userId, SellRequest request);
    Task<ServiceResult<BuySellResponse>> ConfirmPaymentSentAsync(string userId, int buySellId, PaymentMethodType paymentMethod);
    Task<ServiceResult<BuySellResponse>> ConfirmPaymentReceivedAsync(string userId, int buySellId);
    Task<ServiceResult<BuySellResponse>> GetBuySellAsync(int buySellId);
    Task<ServiceResult<IEnumerable<BuySellResponse>>> GetSessionBuySellsAsync(int sessionId);
    Task<ServiceResult<IEnumerable<BuySellResponse>>> GetUserBuySellsAsync(string userId);
    Task<ServiceResult<bool>> CancelBuyAsync(string userId, int buySellId);
    Task<ServiceResult<bool>> CancelSellAsync(string userId, int buySellId);
    Task<ServiceResult<BuySellStatusResponse>> CanBuyAsync(string userId, int sessionId);
    Task<ServiceResult<BuySellStatusResponse>> CanSellAsync(string userId, int sessionId);
}

public class BuySellService : IBuySellService
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly ISessionRepository _sessionRepository;
    private readonly IBuySellRepository _buySellRepository;
    private readonly IServiceBus _serviceBus;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BuySellService> _logger;
    private readonly ISubscriptionHandler _subscriptionHandler;
    private readonly IUserRepository _userRepository;

    public BuySellService(
        UserManager<AspNetUser> userManager,
        ISessionRepository sessionRepository,
        IBuySellRepository buySellRepository,
        IServiceBus serviceBus,
        IConfiguration configuration,
        ILogger<BuySellService> logger,
        ISubscriptionHandler subscriptionHandler,
        IUserRepository userRepository)
    {
        _userManager = userManager;
        _sessionRepository = sessionRepository;
        _buySellRepository = buySellRepository;
        _serviceBus = serviceBus;
        _configuration = configuration;
        _logger = logger;
        _subscriptionHandler = subscriptionHandler;
        _userRepository = userRepository;
    }

    public async Task<ServiceResult<BuySellResponse>> ProcessBuyRequestAsync(string userId, BuyRequest request)
    {
        try
        {
            // First check if the user can buy
            var canBuyResult = await CanBuyAsync(userId, request.SessionId);
            if (!canBuyResult.IsSuccess)
            {
                return ServiceResult<BuySellResponse>.CreateFailure(canBuyResult.Message);
            }

            if (!canBuyResult.Data.IsAllowed)
            {
                return ServiceResult<BuySellResponse>.CreateFailure(canBuyResult.Data.Reason);
            }

            // At this point we know:
            // - Session exists and is valid
            // - User exists and is allowed to buy
            // - Buy window is open
            // - No conflicting BuySells exist
            var session = await _sessionRepository.GetSessionAsync(request.SessionId);
            var buyer = await _userManager.FindByIdAsync(userId);

            // Look for matching sell BuySells
            var matchingSell = await _buySellRepository.FindMatchingSellBuySellAsync(request.SessionId);

            BuySell BuySell, result;
            string message;

            if (matchingSell != null)
            {
                // Get seller's details for team assignment
                var seller = await _userManager.FindByIdAsync(matchingSell.SellerUserId);
                if (seller == null)
                    return ServiceResult<BuySellResponse>.CreateFailure("Seller not found");

                // Get seller's current roster entry to determine team
                var sellerRoster = session.CurrentRosters?.FirstOrDefault(r => r.UserId == seller.Id);
                if (sellerRoster == null)
                    return ServiceResult<BuySellResponse>.CreateFailure("Seller not found in session roster");

                // Match with existing sell BuySell
                BuySell = new BuySell
                {
                    BuySellId = matchingSell.BuySellId,
                    BuyerUserId = userId,
                    UpdateByUserId = userId,
                    TeamAssignment = sellerRoster.TeamAssignment,
                    UpdateDateTime = DateTime.UtcNow,
                    BuyerNote = request.Note,
                };

                message = $"Matched with existing seller: {matchingSell.Seller.FirstName} {matchingSell.Seller.LastName}";
                result = await _buySellRepository.UpdateBuySellAsync(BuySell);
            }
            else
            {
                // Create new buy BuySell
                BuySell = new BuySell
                {
                    SessionId = request.SessionId,
                    BuyerUserId = userId,
                    CreateByUserId = userId,
                    UpdateByUserId = userId,
                    CreateDateTime = DateTime.UtcNow,
                    UpdateDateTime = DateTime.UtcNow,
                    BuyerNote = request.Note,
                    Price = (decimal) session.Cost!,
                };

                message = "Added to buying queue";
                result = await _buySellRepository.CreateBuySellAsync(BuySell);
            }

            //await NotifyBuySellUpdate(result);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing buy request for session {SessionId}", request.SessionId);
            return ServiceResult<BuySellResponse>.CreateFailure($"An error occurred while processing buy request: {ex.Message}");
        }
    }

    public async Task<ServiceResult<BuySellResponse>> ProcessSellRequestAsync(string userId, SellRequest request)
    {
        try
        {
            // First check if the user can sell
            var canSellResult = await CanSellAsync(userId, request.SessionId);
            if (!canSellResult.IsSuccess)
            {
                return ServiceResult<BuySellResponse>.CreateFailure(canSellResult.Message);
            }

            if (!canSellResult.Data.IsAllowed)
            {
                return ServiceResult<BuySellResponse>.CreateFailure(canSellResult.Data.Reason);
            }

            // At this point we know:
            // - Session exists and is valid
            // - User exists and is on the roster
            // - No conflicting BuySells exist
            var session = await _sessionRepository.GetSessionAsync(request.SessionId);
            var seller = await _userManager.FindByIdAsync(userId);

            // Get seller's current roster entry to determine team
            var sellerRoster = session.CurrentRosters?.FirstOrDefault(r => r.UserId == userId);
            if (sellerRoster == null)
            {
                return ServiceResult<BuySellResponse>.CreateFailure("Seller not found in session roster");
            }

            // Look for matching buy BuySells
            var matchingBuy = await _buySellRepository.FindMatchingBuyBuySellAsync(request.SessionId);

            BuySell BuySell, result;
            string message;

            if (matchingBuy != null)
            {
                // Match with existing buy BuySell
                BuySell = new BuySell
                {
                    BuySellId = matchingBuy.BuySellId,
                    SellerUserId = userId,
                    UpdateByUserId = userId,
                    TeamAssignment = sellerRoster.TeamAssignment,
                    UpdateDateTime = DateTime.UtcNow,
                    SellerNote = request.Note,
                };

                message = $"Matched with existing buyer: {matchingBuy.Buyer.FirstName} {matchingBuy.Buyer.LastName}";
                result = await _buySellRepository.UpdateBuySellAsync(BuySell);
            }
            else
            {
                // Create new sell BuySell
                BuySell = new BuySell
                {
                    SessionId = request.SessionId,
                    SellerUserId = userId,
                    CreateByUserId = userId,
                    UpdateByUserId = userId,
                    CreateDateTime = DateTime.UtcNow,
                    UpdateDateTime = DateTime.UtcNow,
                    SellerNote = request.Note,
                    Price = (decimal) session.Cost!,
                    TeamAssignment = sellerRoster.TeamAssignment,
                };

                message = "Added to selling queue";
                result = await _buySellRepository.CreateBuySellAsync(BuySell);
            }

            //await NotifyBuySellUpdate(result);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sell request for session {SessionId}", request.SessionId);
            return ServiceResult<BuySellResponse>.CreateFailure($"An error occurred while processing sell request: {ex.Message}");
        }
    }

    public async Task<ServiceResult<BuySellResponse>> ConfirmPaymentSentAsync(string userId, int buySellId, PaymentMethodType paymentMethod)
    {
        try
        {
            var BuySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (BuySell == null)
                return ServiceResult<BuySellResponse>.CreateFailure("BuySell not found");

            // Verify user is part of the BuySell
            if (BuySell.BuyerUserId == userId)
                return ServiceResult<BuySellResponse>.CreateFailure("Not authorized to confirm payment sent for this BuySell");

            BuySell.UpdateDateTime = DateTime.UtcNow;
            BuySell.UpdateByUserId = userId;
            BuySell.PaymentMethod = (int) paymentMethod;
            BuySell.PaymentSent = true;

            var result = await _buySellRepository.UpdateBuySellAsync(BuySell);
            //await NotifyBuySellUpdate(result);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), "Payment confirmed sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment sent for BuySell {BuySellId}", buySellId);
            return ServiceResult<BuySellResponse>.CreateFailure($"An error occurred while confirming payment sent: {ex.Message}");
        }
    }

    public async Task<ServiceResult<BuySellResponse>> ConfirmPaymentReceivedAsync(string userId, int buySellId)
    {
        try
        {
            var BuySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (BuySell == null)
                return ServiceResult<BuySellResponse>.CreateFailure("BuySell not found");

            // Verify user is part of the BuySell
            if (BuySell.SellerUserId == userId)
                return ServiceResult<BuySellResponse>.CreateFailure("Not authorized to confirm payment received for this BuySell");

            BuySell.UpdateDateTime = DateTime.UtcNow;
            BuySell.UpdateByUserId = userId;
            BuySell.PaymentReceived = true;

            var result = await _buySellRepository.UpdateBuySellAsync(BuySell);
            //await NotifyBuySellUpdate(result);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), "Payment confirmed received");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment received for BuySell {BuySellId}", buySellId);
            return ServiceResult<BuySellResponse>.CreateFailure($"An error occurred while confirming payment received: {ex.Message}");
        }
    }

    public async Task<ServiceResult<BuySellResponse>> GetBuySellAsync(int buySellId)
    {
        try
        {
            var BuySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (BuySell == null)
                return ServiceResult<BuySellResponse>.CreateFailure("BuySell not found");

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(BuySell));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BuySell {BuySellId}", buySellId);
            return ServiceResult<BuySellResponse>.CreateFailure($"An error occurred while retrieving BuySell: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<BuySellResponse>>> GetSessionBuySellsAsync(int sessionId)
    {
        try
        {
            var BuySells = await _buySellRepository.GetSessionBuySellsAsync(sessionId);
            var responses = await Task.WhenAll(BuySells.Select(t => MapBuySellToResponse(t)));

            return ServiceResult<IEnumerable<BuySellResponse>>.CreateSuccess(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BuySells for session {SessionId}", sessionId);
            return ServiceResult<IEnumerable<BuySellResponse>>.CreateFailure($"An error occurred while retrieving session BuySells: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<BuySellResponse>>> GetUserBuySellsAsync(string userId)
    {
        try
        {
            var BuySells = await _buySellRepository.GetUserBuySellsAsync(userId);
            var responses = await Task.WhenAll(BuySells.Select(t => MapBuySellToResponse(t)));

            return ServiceResult<IEnumerable<BuySellResponse>>.CreateSuccess(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BuySells for user {UserId}", userId);
            return ServiceResult<IEnumerable<BuySellResponse>>.CreateFailure($"An error occurred while retrieving user BuySells: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> CancelBuyAsync(string userId, int buySellId)
    {
        try
        {
            var buySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (buySell == null)
                return ServiceResult<bool>.CreateFailure("BuySell not found");

            // Verify user is the buyer
            if (buySell.BuyerUserId != userId)
                return ServiceResult<bool>.CreateFailure("Buyer not authorized to cancel this BuySell");

            // Only allow cancellation if buyer has not bought
            if (buySell.SellerUserId != null)
                return ServiceResult<bool>.CreateFailure("Buyer cannot cancel spot that is already bought");

            var result = await _buySellRepository.DeleteBuySellAsync(buySellId);

            return ServiceResult<bool>.CreateSuccess(true, $"Buyer: {buySell.Buyer.FirstName} {buySell.Buyer.LastName} cancelled BuySell");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling BuySell {BuySellId}", buySellId);
            return ServiceResult<bool>.CreateFailure($"An error occurred while cancelling BuySell: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> CancelSellAsync(string userId, int buySellId)
    {
        try
        {
            var buySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (buySell == null)
                return ServiceResult<bool>.CreateFailure("BuySell not found");

            // Verify user is the seller
            if (buySell.SellerUserId != userId)
                return ServiceResult<bool>.CreateFailure("Seller not authorized to cancel this BuySell");

            // Only allow cancellation if seller has not sold
            if (buySell.BuyerUserId != null)
                return ServiceResult<bool>.CreateFailure("Seller cannot cancel spot that is already sold");

            var result = await _buySellRepository.DeleteBuySellAsync(buySellId);

            return ServiceResult<bool>.CreateSuccess(true, $"Seller: {buySell.Seller.FirstName} {buySell.Seller.LastName} cancelled BuySell");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling BuySell {BuySellId}", buySellId);
            return ServiceResult<bool>.CreateFailure($"An error occurred while cancelling BuySell: {ex.Message}");
        }
    }

    public async Task<ServiceResult<BuySellStatusResponse>> CanBuyAsync(string userId, int sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetSessionAsync(sessionId);
            if (session == null)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "Session not found"
                });
            }

            var buyer = await _userManager.FindByIdAsync(userId);
            if (buyer == null)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "User not found"
                });
            }

            // Check if session is in the past
            var currentPacificTime = TimeZoneUtils.GetCurrentPacificTime();
            if (session.SessionDate <= currentPacificTime)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "Cannot buy for a session that has already started"
                });
            }

            // Check roster status
            if (session.CurrentRosters?.Any(r => r.UserId == userId && r.IsPlaying) == true)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "You are already on the roster for this session"
                });
            }


            // Check buy window based on user status
            var user = session.CurrentRosters?.FirstOrDefault(r => r.UserId == userId);
            DateTime buyWindow;
            string windowType;
            if (buyer.PreferredPlus)
            {
                buyWindow = session.BuyWindowPreferredPlus;
                windowType = "Preferred Plus";
            }
            else if (buyer.Preferred)
            {
                buyWindow = session.BuyWindowPreferred;
                windowType = "Preferred";
            }
            else
            {
                buyWindow = session.BuyWindow;
                windowType = "Regular";
            }

            if (currentPacificTime < buyWindow)
            {
                var timeUntilOpen = buyWindow - currentPacificTime;
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = $"{windowType} buy window is not open yet",
                    TimeUntilAllowed = timeUntilOpen
                });
            }

            // Check existing BuySells
            var userBuySells = await _buySellRepository.GetUserBuySellsAsync(sessionId, userId);
            var hasActiveBuySell = userBuySells.Any(t => t.BuyerUserId == userId && t.SellerUserId == null);
            if (hasActiveBuySell)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "You already have an active Buy for this session"
                });
            }

            return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
            {
                IsAllowed = true,
                Reason = "You can buy a spot for this session"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking buy eligibility for user {UserId} in session {SessionId}", userId, sessionId);
            return ServiceResult<BuySellStatusResponse>.CreateFailure("An error occurred while checking buy eligibility");
        }
    }

    public async Task<ServiceResult<BuySellStatusResponse>> CanSellAsync(string userId, int sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetSessionAsync(sessionId);
            if (session == null)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "Session not found"
                });
            }

            // Check if session is in the past
            var currentPacificTime = TimeZoneUtils.GetCurrentPacificTime();
            if (session.SessionDate <= currentPacificTime)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "Cannot sell for a session that has already started"
                });
            }

            // Check if user is on roster
            if (session.CurrentRosters?.Any(r => r.UserId == userId && !r.IsPlaying) == true)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "You must be on the roster to sell your spot"
                });
            }

            // Check existing BuySells for an active open sale
            var userBuySells = await _buySellRepository.GetUserBuySellsAsync(sessionId, userId);
            var hasActiveBuySell = userBuySells.Any(t => t.SellerUserId == userId && t.BuyerUserId == null);
            if (hasActiveBuySell)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "You already have an active Sell for this session"
                });
            }

            return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
            {
                IsAllowed = true,
                Reason = "You can sell your spot for this session"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking sell eligibility for user {UserId} in session {SessionId}", userId, sessionId);
            return ServiceResult<BuySellStatusResponse>.CreateFailure("An error occurred while checking sell eligibility");
        }
    }

    private async Task<BuySellResponse> MapBuySellToResponse(BuySell buySell)
    {
        var buyer = buySell.BuyerUserId != null ? await _userRepository.GetUserAsync(buySell.BuyerUserId) : null;
        var seller = buySell.SellerUserId != null ? await _userRepository.GetUserAsync(buySell.SellerUserId) : null;

        return new BuySellResponse
        {
            BuySellId = buySell.BuySellId,
            TeamAssignment = buySell.TeamAssignment,
            BuyerNote = buySell.BuyerNote,
            SellerNote = buySell.SellerNote,
            Price = buySell.Price,
            PaymentMethod = (PaymentMethodType) buySell.PaymentMethod,
            PaymentSent = buySell.PaymentSent,
            PaymentReceived = buySell.PaymentReceived,
            CreateDateTime = buySell.CreateDateTime,
            UpdateDateTime = buySell.UpdateDateTime,
            CreateByUserId = buySell.CreateByUserId,
            UpdateByUserId = buySell.UpdateByUserId,
            Buyer = buyer != null ? new UserDetailedResponse
            {
                Id = buyer.Id,
                UserName = buyer.UserName,
                FirstName = buyer.FirstName,
                LastName = buyer.LastName,
                Email = buyer.Email!,
                PhotoUrl = buyer.PhotoUrl,
                Rating = buyer.Rating,
                Active = buyer.Active,
                Preferred = buyer.Preferred,
                PreferredPlus = buyer.PreferredPlus
            } : null,
            Seller = seller != null ? new UserDetailedResponse
            {
                Id = seller.Id,
                UserName = seller.UserName,
                FirstName = seller.FirstName,
                LastName = seller.LastName,
                Email = seller.Email!,
                PhotoUrl = seller.PhotoUrl,
                Rating = seller.Rating,
                Active = seller.Active,
                Preferred = seller.Preferred,
                PreferredPlus = seller.PreferredPlus
            } : null,
            QueuePosition = await _buySellRepository.GetQueuePositionAsync(buySell.BuySellId) ?? 0,
        };
    }
}
