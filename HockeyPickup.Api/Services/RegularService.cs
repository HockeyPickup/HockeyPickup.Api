using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Services;

public interface IRegularService
{
    Task<ServiceResult<RegularSetDetailedResponse>> DuplicateRegularSet(DuplicateRegularSetRequest request);
    Task<ServiceResult<RegularSetDetailedResponse>> UpdateRegularSet(UpdateRegularSetRequest request);
    Task<ServiceResult<RegularSetDetailedResponse>> UpdateRegularPosition(int regularSetId, string userId, PositionPreference newPosition);
    Task<ServiceResult<RegularSetDetailedResponse>> UpdateRegularTeam(int regularSetId, string userId, TeamAssignment newTeamAssignment);
    Task<ServiceResult> DeleteRegularSet(int regularSetId);
    Task<ServiceResult<RegularSetDetailedResponse>> AddRegular(AddRegularRequest request);
    Task<ServiceResult<RegularSetDetailedResponse>> DeleteRegular(DeleteRegularRequest request);
    Task<ServiceResult<RegularSetDetailedResponse>> CreateRegularSet(CreateRegularSetRequest request);
}

public class RegularService : IRegularService
{
    private readonly UserManager<AspNetUser> _userManager;
    private readonly IRegularRepository _regularRepository;
    private readonly IServiceBus _serviceBus;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;

    public RegularService(UserManager<AspNetUser> userManager, IRegularRepository regularRepository, IServiceBus serviceBus, IConfiguration configuration, ILogger<UserService> logger)
    {
        _userManager = userManager;
        _regularRepository = regularRepository;
        _serviceBus = serviceBus;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ServiceResult<RegularSetDetailedResponse>> DuplicateRegularSet(DuplicateRegularSetRequest request)
    {
        try
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(request.Description))
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Description is required");

            // Check if source regular set exists
            var sourceSet = await _regularRepository.GetRegularSetAsync(request.RegularSetId);
            if (sourceSet == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"Regular set with Id {request.RegularSetId} not found");

            // Perform the duplication
            var newRegularSet = await _regularRepository.DuplicateRegularSetAsync(request.RegularSetId, request.Description);

            if (newRegularSet == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Failed to create new regular set");

            // Log the action
            _logger.LogInformation("Regular set {SourceId} duplicated to new set {NewId}", request.RegularSetId, newRegularSet.RegularSetId);

            return ServiceResult<RegularSetDetailedResponse>.CreateSuccess(newRegularSet, $"Regular set duplicated successfully with Id {newRegularSet.RegularSetId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating regular set {RegularSetId}", request.RegularSetId);
            return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"An error occurred while duplicating the regular set. Error: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RegularSetDetailedResponse>> UpdateRegularSet(UpdateRegularSetRequest request)
    {
        try
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(request.Description))
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Description is required");

            if (request.DayOfWeek is < 0 or > 6)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Day of week must be between 0 and 6");

            // Update the regular set
            var updatedSet = await _regularRepository.UpdateRegularSetAsync(request.RegularSetId, request.Description, request.DayOfWeek, request.Archived);
            if (updatedSet == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"Failed to update regular set with Id {request.RegularSetId}");

            // Log the action
            _logger.LogInformation("Regular set {Id} updated successfully", request.RegularSetId);

            return ServiceResult<RegularSetDetailedResponse>.CreateSuccess(updatedSet, $"Regular set {request.RegularSetId} updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating regular set {RegularSetId}", request.RegularSetId);
            return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"An error occurred while updating the regular set. Error: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RegularSetDetailedResponse>> UpdateRegularPosition(int regularSetId, string userId, PositionPreference newPosition)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("User not found");
            }

            var regularSet = await _regularRepository.GetRegularSetAsync(regularSetId);
            if (regularSet == null)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Regular set not found");
            }

            var currentRegular = regularSet.Regulars?.FirstOrDefault(r => r.UserId == userId);
            if (currentRegular == null)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("User is not part of this Regular set");
            }

            if (currentRegular.PositionPreference == newPosition)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("New position is the same as the current position");
            }

            var updatedSet = await _regularRepository.UpdatePlayerPositionAsync(regularSetId, userId, newPosition);
            if (updatedSet == null)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Failed to update player position");
            }

            return ServiceResult<RegularSetDetailedResponse>.CreateSuccess(updatedSet,
                $"{user.FirstName} {user.LastName}'s position preference updated to {newPosition.ParsePositionName()}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating regular player position for set: {RegularSetId}, user: {UserId}", regularSetId, userId);
            return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"An error occurred updating player position: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RegularSetDetailedResponse>> UpdateRegularTeam(int regularSetId, string userId, TeamAssignment newTeamAssignment)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("User not found");
            }

            var regularSet = await _regularRepository.GetRegularSetAsync(regularSetId);
            if (regularSet == null)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Regular set not found");
            }

            var currentRegular = regularSet.Regulars?.FirstOrDefault(r => r.UserId == userId);
            if (currentRegular == null)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("User is not part of this Regular set");
            }

            if (currentRegular.TeamAssignment == newTeamAssignment)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("New team assignment is the same as the current team assignment");
            }

            var updatedSet = await _regularRepository.UpdatePlayerTeamAsync(regularSetId, userId, newTeamAssignment);
            if (updatedSet == null)
            {
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Failed to update player team");
            }

            return ServiceResult<RegularSetDetailedResponse>.CreateSuccess(updatedSet,
                $"{user.FirstName} {user.LastName}'s team assignment updated to {newTeamAssignment}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating regular player team for set: {RegularSetId}, user: {UserId}", regularSetId, userId);
            return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"An error occurred updating player team: {ex.Message}");
        }
    }

    public async Task<ServiceResult> DeleteRegularSet(int regularSetId)
    {
        try
        {
            var (Success, Message) = await _regularRepository.DeleteRegularSetAsync(regularSetId);
            if (!Success)
            {
                return ServiceResult.CreateFailure(Message);
            }

            _logger.LogInformation("Regular set {RegularSetId} deleted successfully", regularSetId);
            return ServiceResult.CreateSuccess("Regular set deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting regular set {RegularSetId}", regularSetId);
            return ServiceResult.CreateFailure($"An error occurred while deleting the regular set: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RegularSetDetailedResponse>> AddRegular(AddRegularRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("User not found");

            var regularSet = await _regularRepository.GetRegularSetAsync(request.RegularSetId);
            if (regularSet == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Regular set not found");

            if (regularSet.Regulars?.Any(r => r.UserId == request.UserId) == true)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("User is already in this Regular set");

            var updatedSet = await _regularRepository.AddPlayerAsync(request.RegularSetId, request.UserId, request.TeamAssignment, request.PositionPreference);
            if (updatedSet == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Failed to add player to Regular set");

            return ServiceResult<RegularSetDetailedResponse>.CreateSuccess(updatedSet,
                $"{user.FirstName} {user.LastName} added to Regular set as {request.TeamAssignment.GetDisplayName()}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding regular player to set: {RegularSetId}, user: {UserId}", request.RegularSetId, request.UserId);
            return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"An error occurred adding player: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RegularSetDetailedResponse>> DeleteRegular(DeleteRegularRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("User not found");

            var regularSet = await _regularRepository.GetRegularSetAsync(request.RegularSetId);
            if (regularSet == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Regular set not found");

            if (regularSet.Regulars?.Any(r => r.UserId == request.UserId) != true)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("User is not part of this Regular set");

            var updatedSet = await _regularRepository.RemovePlayerAsync(request.RegularSetId, request.UserId);
            if (updatedSet == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Failed to remove player from Regular set");

            return ServiceResult<RegularSetDetailedResponse>.CreateSuccess(updatedSet,
                $"{user.FirstName} {user.LastName} removed from Regular set");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing regular player from set: {RegularSetId}, user: {UserId}", request.RegularSetId, request.UserId);
            return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"An error occurred removing player: {ex.Message}");
        }
    }

    public async Task<ServiceResult<RegularSetDetailedResponse>> CreateRegularSet(CreateRegularSetRequest request)
    {
        try
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(request.Description))
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Description is required");

            if (request.DayOfWeek is < 0 or > 6)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Day of week must be between 0 and 6");

            // Create new regular set using repository
            var newSet = await _regularRepository.CreateRegularSetAsync(request.Description, request.DayOfWeek);
            if (newSet == null)
                return ServiceResult<RegularSetDetailedResponse>.CreateFailure("Failed to create regular set");

            _logger.LogInformation("Regular set created with Id {RegularSetId}", newSet.RegularSetId);

            return ServiceResult<RegularSetDetailedResponse>.CreateSuccess(newSet,
                $"Regular set created successfully with Id {newSet.RegularSetId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating regular set");
            return ServiceResult<RegularSetDetailedResponse>.CreateFailure($"An error occurred while creating the regular set: {ex.Message}");
        }
    }
}
