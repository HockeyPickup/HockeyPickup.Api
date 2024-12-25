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
}
