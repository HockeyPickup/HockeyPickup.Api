using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Data.Repositories;

public interface IRegularRepository
{
    Task<IEnumerable<RegularSetDetailedResponse>> GetRegularSetsAsync();
    Task<RegularSetDetailedResponse?> GetRegularSetAsync(int regularSetId);
}
