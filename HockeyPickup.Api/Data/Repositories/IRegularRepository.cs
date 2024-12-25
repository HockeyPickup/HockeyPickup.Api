using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface IRegularRepository
{
    Task<IEnumerable<RegularSetDetailedResponse>> GetRegularSetsAsync();
    Task<RegularSetDetailedResponse?> GetRegularSetAsync(int regularSetId);
    Task<RegularSetDetailedResponse?> DuplicateRegularSetAsync(int regularSetId, string description);
    Task<RegularSetDetailedResponse?> UpdateRegularSetAsync(int regularSetId, string description, int dayOfWeek, bool archived);
}
