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
    Task<ServiceResult<BuySellResponse>> UnconfirmPaymentSentAsync(string userId, int buySellId);
    Task<ServiceResult<BuySellResponse>> UnconfirmPaymentReceivedAsync(string userId, int buySellId);
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

            BuySell buySell, result;
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
                buySell = matchingSell;
                buySell.BuyerUserId = userId;
                buySell.UpdateByUserId = userId;
                buySell.UpdateDateTime = DateTime.UtcNow;
                buySell.BuyerNote = request.Note;

                message = $"{buyer.FirstName} {buyer.LastName} BOUGHT spot from seller: {matchingSell.Seller.FirstName} {matchingSell.Seller.LastName}";
                result = await _buySellRepository.UpdateBuySellAsync(buySell, message);
            }
            else
            {
                // Create new buy BuySell
                buySell = new BuySell
                {
                    SessionId = request.SessionId,
                    BuyerUserId = userId,
                    TeamAssignment = TeamAssignment.TBD,
                    CreateByUserId = userId,
                    UpdateByUserId = userId,
                    CreateDateTime = DateTime.UtcNow,
                    UpdateDateTime = DateTime.UtcNow,
                    BuyerNote = request.Note,
                    Price = session.Cost,
                    Buyer = buyer,
                    PaymentSent = false,
                    PaymentReceived = false,
                    SellerNoteFlagged = false,
                    BuyerNoteFlagged = false,
                    PaymentMethod = null
                };

                message = $"{buyer.FirstName} {buyer.LastName} added to BUYING queue";
                result = await _buySellRepository.CreateBuySellAsync(buySell, message);
            }

            //await NotifyBuySellUpdate(result);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), message);
        }
        catch (Exception ex)
        {
            var msg = $"Error processing buy request for session {request.SessionId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellResponse>.CreateFailure(msg);
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

            BuySell buySell, result;
            string message;

            if (matchingBuy != null)
            {
                // Match with existing buy BuySell
                buySell = matchingBuy;
                buySell.SellerUserId = userId;
                buySell.UpdateByUserId = userId;
                buySell.UpdateDateTime = DateTime.UtcNow;
                buySell.SellerNote = request.Note;

                message = $"{seller.FirstName} {seller.LastName} SOLD spot to buyer: {matchingBuy.Buyer.FirstName} {matchingBuy.Buyer.LastName}";
                result = await _buySellRepository.UpdateBuySellAsync(buySell, message);
            }
            else
            {
                // Create new sell BuySell
                buySell = new BuySell
                {
                    SessionId = request.SessionId,
                    SellerUserId = userId,
                    CreateByUserId = userId,
                    UpdateByUserId = userId,
                    CreateDateTime = DateTime.UtcNow,
                    UpdateDateTime = DateTime.UtcNow,
                    SellerNote = request.Note,
                    Price = session.Cost,
                    TeamAssignment = sellerRoster.TeamAssignment,
                    Seller = seller,
                    PaymentSent = false,
                    PaymentReceived = false,
                    SellerNoteFlagged = false,
                    BuyerNoteFlagged = false,
                    PaymentMethod = null
                };

                message = $"{seller.FirstName} {seller.LastName} added to SELLING queue";
                result = await _buySellRepository.CreateBuySellAsync(buySell, message);
            }

            //await NotifyBuySellUpdate(result);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), message);
        }
        catch (Exception ex)
        {
            var msg = $"Error processing sell request for session {request.SessionId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellResponse>.CreateFailure(msg);
        }
    }

    public async Task<ServiceResult<BuySellResponse>> ConfirmPaymentSentAsync(string userId, int buySellId, PaymentMethodType paymentMethod)
    {
        try
        {
            var buySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (buySell == null)
                return ServiceResult<BuySellResponse>.CreateFailure("BuySell not found");

            // Verify user is part of the BuySell
            if (buySell.BuyerUserId != userId)
                return ServiceResult<BuySellResponse>.CreateFailure("Not authorized to confirm payment sent for this BuySell");

            buySell.UpdateDateTime = DateTime.UtcNow;
            buySell.UpdateByUserId = userId;
            buySell.PaymentMethod = paymentMethod;
            buySell.PaymentSent = true;

            var message = $"{buySell.Buyer.FirstName} {buySell.Buyer.LastName} confirmed PAYMENT sent";
            var result = await _buySellRepository.UpdateBuySellAsync(buySell, message);
            //await NotifyBuySellUpdate(result);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), message);
        }
        catch (Exception ex)
        {
            var msg = $"Error confirming payment sent request for BuySell {buySellId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellResponse>.CreateFailure(msg);
        }
    }

    public async Task<ServiceResult<BuySellResponse>> ConfirmPaymentReceivedAsync(string userId, int buySellId)
    {
        try
        {
            var buySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (buySell == null)
                return ServiceResult<BuySellResponse>.CreateFailure("BuySell not found");

            // Verify user is part of the BuySell
            if (buySell.SellerUserId != userId)
                return ServiceResult<BuySellResponse>.CreateFailure("Not authorized to confirm payment received for this BuySell");

            buySell.UpdateDateTime = DateTime.UtcNow;
            buySell.UpdateByUserId = userId;
            buySell.PaymentReceived = true;

            var message = $"{buySell.Seller.FirstName} {buySell.Seller.LastName} confirmed PAYMENT received";
            var result = await _buySellRepository.UpdateBuySellAsync(buySell, message);
            //await NotifyBuySellUpdate(result);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), message);
        }
        catch (Exception ex)
        {
            var msg = $"Error confirming payment received request for BuySell {buySellId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellResponse>.CreateFailure(msg);
        }
    }

    public async Task<ServiceResult<BuySellResponse>> UnconfirmPaymentSentAsync(string userId, int buySellId)
    {
        try
        {
            var buySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (buySell == null)
                return ServiceResult<BuySellResponse>.CreateFailure("BuySell not found");

            // Verify user is part of the BuySell
            if (buySell.BuyerUserId != userId)
                return ServiceResult<BuySellResponse>.CreateFailure("Not authorized to unconfirm payment sent for this BuySell");

            buySell.UpdateDateTime = DateTime.UtcNow;
            buySell.UpdateByUserId = userId;
            buySell.PaymentSent = false;
            buySell.PaymentMethod = null; // Clear the payment method when unconfirming

            var message = $"{buySell.Buyer.FirstName} {buySell.Buyer.LastName} unconfirmed PAYMENT sent";
            var result = await _buySellRepository.UpdateBuySellAsync(buySell, message);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), message);
        }
        catch (Exception ex)
        {
            var msg = $"Error unconfirming payment sent for BuySell {buySellId}: {(ex.Message.Contains("inner exception") ? ex.InnerException?.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellResponse>.CreateFailure(msg);
        }
    }

    public async Task<ServiceResult<BuySellResponse>> UnconfirmPaymentReceivedAsync(string userId, int buySellId)
    {
        try
        {
            var buySell = await _buySellRepository.GetBuySellAsync(buySellId);
            if (buySell == null)
                return ServiceResult<BuySellResponse>.CreateFailure("BuySell not found");

            // Verify user is part of the BuySell
            if (buySell.SellerUserId != userId)
                return ServiceResult<BuySellResponse>.CreateFailure("Not authorized to unconfirm payment received for this BuySell");

            buySell.UpdateDateTime = DateTime.UtcNow;
            buySell.UpdateByUserId = userId;
            buySell.PaymentReceived = false;

            var message = $"{buySell.Seller.FirstName} {buySell.Seller.LastName} unconfirmed PAYMENT received";
            var result = await _buySellRepository.UpdateBuySellAsync(buySell, message);

            return ServiceResult<BuySellResponse>.CreateSuccess(await MapBuySellToResponse(result), message);
        }
        catch (Exception ex)
        {
            var msg = $"Error unconfirming payment received for BuySell {buySellId}: {(ex.Message.Contains("inner exception") ? ex.InnerException?.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellResponse>.CreateFailure(msg);
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
            var msg = $"Error getting BuySell {buySellId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellResponse>.CreateFailure(msg);
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
            var msg = $"Error getting BuySells for Session {sessionId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<IEnumerable<BuySellResponse>>.CreateFailure(msg);
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
            var msg = $"Error getting BuySells for User {userId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<IEnumerable<BuySellResponse>>.CreateFailure(msg);
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
                return ServiceResult<bool>.CreateFailure("Not authorized to cancel this BuySell");

            // Only allow cancellation if buyer has not bought
            if (buySell.SellerUserId != null)
                return ServiceResult<bool>.CreateFailure("Cannot cancel spot that is already bought");

            var message = $"Buyer: {buySell.Buyer.FirstName} {buySell.Buyer.LastName} cancelled BuySell";
            var result = await _buySellRepository.DeleteBuySellAsync(buySellId, message);

            return ServiceResult<bool>.CreateSuccess(true, message);
        }
        catch (Exception ex)
        {
            var msg = $"Error cancelling Buy BuySell {buySellId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<bool>.CreateFailure(msg);
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
                return ServiceResult<bool>.CreateFailure("Not authorized to cancel this BuySell");

            // Only allow cancellation if seller has not sold
            if (buySell.BuyerUserId != null)
                return ServiceResult<bool>.CreateFailure("Cannot cancel spot that is already sold");

            var message = $"Seller: {buySell.Seller.FirstName} {buySell.Seller.LastName} cancelled BuySell";
            var result = await _buySellRepository.DeleteBuySellAsync(buySellId, message);

            return ServiceResult<bool>.CreateSuccess(true, message);
        }
        catch (Exception ex)
        {
            var msg = $"Error cancelling Sell BuySell {buySellId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<bool>.CreateFailure(msg);
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

            // Check existing BuySells for active Buy
            var userBuySells = await _buySellRepository.GetUserBuySellsAsync(sessionId, userId);
            var hasActiveBuySellBuy = userBuySells.Any(t => t.BuyerUserId == userId && t.SellerUserId == null);
            if (hasActiveBuySellBuy)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "You already have an active Buy for this session"
                });
            }

            // Check existing BuySells for an active Sell
            var hasActiveBuySellSell = userBuySells.Any(t => t.SellerUserId == userId && t.BuyerUserId == null);
            if (hasActiveBuySellSell)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "You have an active Sell for this session"
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

            return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
            {
                IsAllowed = true,
                Reason = "You can buy a spot for this session"
            });
        }
        catch (Exception ex)
        {
            var msg = $"Error checking buy eligibility for user {userId} in session {sessionId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellStatusResponse>.CreateFailure(msg);
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

            // Check existing BuySells for active Buy
            var userBuySells = await _buySellRepository.GetUserBuySellsAsync(sessionId, userId);
            var hasActiveBuySellBuy = userBuySells.Any(t => t.BuyerUserId == userId && t.SellerUserId == null);
            if (hasActiveBuySellBuy)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "You have an active Buy for this session"
                });
            }

            // Check existing BuySells for an active Sell
            var hasActiveBuySellSell = userBuySells.Any(t => t.SellerUserId == userId && t.BuyerUserId == null);
            if (hasActiveBuySellSell)
            {
                return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
                {
                    IsAllowed = false,
                    Reason = "You already have an active Sell for this session"
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

            return ServiceResult<BuySellStatusResponse>.CreateSuccess(new BuySellStatusResponse
            {
                IsAllowed = true,
                Reason = "You can sell your spot for this session"
            });
        }
        catch (Exception ex)
        {
            var msg = $"Error checking sell eligibility for user {userId} in session {sessionId}: {(ex.Message.Contains("inner exception") ? ex.InnerException.Message : ex.Message)}";
            _logger.LogError(ex, msg);
            return ServiceResult<BuySellStatusResponse>.CreateFailure(msg);
        }
    }

    private async Task<BuySellResponse> MapBuySellToResponse(BuySell buySell)
    {
        var buyer = buySell.BuyerUserId != null ? await _userRepository.GetUserAsync(buySell.BuyerUserId) : null;
        var seller = buySell.SellerUserId != null ? await _userRepository.GetUserAsync(buySell.SellerUserId) : null;

        return new BuySellResponse
        {
            BuySellId = buySell.BuySellId,
            BuyerUserId = buySell.BuyerUserId,
            SellerUserId = buySell.SellerUserId,
            SellerNote = buySell.SellerNote,
            BuyerNote = buySell.BuyerNote,
            PaymentSent = buySell.PaymentSent,
            PaymentReceived = buySell.PaymentReceived,
            CreateDateTime = buySell.CreateDateTime,
            TeamAssignment = buySell.TeamAssignment,
            UpdateDateTime = buySell.UpdateDateTime,
            Price = buySell.Price ?? 0m,
            CreateByUserId = buySell.CreateByUserId,
            UpdateByUserId = buySell.UpdateByUserId,
            PaymentMethod = buySell.PaymentMethod.HasValue ? buySell.PaymentMethod.Value : PaymentMethodType.Unknown,
            TransactionStatus = buySell.TransactionStatus,
            SellerNoteFlagged = buySell.SellerNoteFlagged,
            BuyerNoteFlagged = buySell.BuyerNoteFlagged,
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
