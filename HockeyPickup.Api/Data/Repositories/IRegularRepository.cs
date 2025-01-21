using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface IRegularRepository
{
    Task<IEnumerable<RegularSetDetailedResponse>> GetRegularSetsAsync();
    Task<RegularSetDetailedResponse?> GetRegularSetAsync(int regularSetId);
    Task<RegularSetDetailedResponse?> DuplicateRegularSetAsync(int regularSetId, string description);
    Task<RegularSetDetailedResponse?> UpdateRegularSetAsync(int regularSetId, string description, int dayOfWeek, bool archived);
    Task<RegularSetDetailedResponse?> UpdatePlayerPositionAsync(int regularSetId, string userId, int position);
    Task<RegularSetDetailedResponse?> UpdatePlayerTeamAsync(int regularSetId, string userId, int team);
    Task<(bool Success, string Message)> DeleteRegularSetAsync(int regularSetId);
    Task<RegularSetDetailedResponse?> AddPlayerAsync(int regularSetId, string userId, int teamAssignment, int positionPreference);
    Task<RegularSetDetailedResponse?> RemovePlayerAsync(int regularSetId, string userId);
    Task<RegularSetDetailedResponse?> CreateRegularSetAsync(string description, int dayOfWeek);
}
