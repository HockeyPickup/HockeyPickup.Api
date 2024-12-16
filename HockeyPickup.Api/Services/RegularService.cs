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
}
