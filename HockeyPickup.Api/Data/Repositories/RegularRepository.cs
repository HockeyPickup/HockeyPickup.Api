using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Models.Responses;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace HockeyPickup.Api.Data.Repositories;

public class RegularRepository : IRegularRepository
{
    private readonly HockeyPickupContext _context;
    private readonly ILogger<RegularRepository> _logger;
    private readonly IDbFacade _db;

    public RegularRepository(HockeyPickupContext context, ILogger<RegularRepository> logger, IDbFacade? db = null)
    {
        _context = context;
        _logger = logger;
        _db = db ?? new DbFacade(context.Database);
    }

    public async Task<IEnumerable<RegularSetDetailedResponse>> GetRegularSetsAsync()
    {
        var regularSets = await _context.RegularSets!
            .Include(rs => rs.Regulars)
                .ThenInclude(r => r.User)
            .OrderByDescending(rs => rs.CreateDateTime)
            .AsSplitQuery()
            .ToListAsync();

        return regularSets.Select(rs => MapToDetailedResponse(rs)).ToList();
    }

    public async Task<RegularSetDetailedResponse?> GetRegularSetAsync(int regularSetId)
    {
        var regularSet = await _context.RegularSets!
            .Include(rs => rs.Regulars)
                .ThenInclude(r => r.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(rs => rs.RegularSetId == regularSetId);

        return regularSet != null ? MapToDetailedResponse(regularSet) : null;
    }

    private static RegularSetDetailedResponse MapToDetailedResponse(RegularSet regularSet)
    {
        if (regularSet == null) return null;

        return new RegularSetDetailedResponse
        {
            RegularSetId = regularSet.RegularSetId,
            Description = regularSet.Description,
            DayOfWeek = regularSet.DayOfWeek,
            Archived = regularSet.Archived,
            CreateDateTime = regularSet.CreateDateTime,
            Regulars = MapRegulars(regularSet.Regulars)
        };
    }

    private static List<RegularDetailedResponse> MapRegulars(ICollection<Regular> regulars)
    {
        if (regulars == null) return new List<RegularDetailedResponse>();

        return regulars.Select(r => new RegularDetailedResponse
        {
            RegularSetId = r.RegularSetId,
            UserId = r.UserId,
            TeamAssignment = r.TeamAssignment,
            PositionPreference = r.PositionPreference,
            User = r.User.ToDetailedResponse()
        }).ToList();
    }

    public async Task<RegularSetDetailedResponse?> DuplicateRegularSetAsync(int regularSetId, string description)
    {
        try
        {
            var newIdParameter = new SqlParameter
            {
                ParameterName = "@NewRegularSetId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            await _db.ExecuteSqlRawAsync(
                "EXEC CopyRoster @RegularSetId, @NewRosterDescription, @NewRegularSetId OUTPUT",
                new[]
                {
                    new SqlParameter("@RegularSetId", regularSetId),
                    new SqlParameter("@NewRosterDescription", description),
                    newIdParameter
                });

            var newRegularSetId = (int) newIdParameter.Value;
            return await GetRegularSetAsync(newRegularSetId);
        }
        catch
        {
            return null;
        }
    }

    public async Task<RegularSetDetailedResponse?> UpdateRegularSetAsync(int regularSetId, string description, int dayOfWeek, bool archived)
    {
        try
        {
            var regularSet = await _context.RegularSets!
                .FirstOrDefaultAsync(rs => rs.RegularSetId == regularSetId);

            if (regularSet == null)
                return null;

            regularSet.Description = description;
            regularSet.DayOfWeek = dayOfWeek;
            regularSet.Archived = archived;

            await _context.SaveChangesAsync();

            // Fetch the updated entity with all related data
            return await GetRegularSetAsync(regularSetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating regular set {RegularSetId}", regularSetId);
            return null;
        }
    }

    public async Task<RegularSetDetailedResponse?> UpdatePlayerPositionAsync(int regularSetId, string userId, int position)
    {
        try
        {
            var regular = await _context.Regulars!
                .FirstOrDefaultAsync(r => r.RegularSetId == regularSetId && r.UserId == userId);

            if (regular == null)
                return null;

            regular.PositionPreference = position;
            await _context.SaveChangesAsync();

            // Fetch and return updated regular set details
            return await GetRegularSetAsync(regularSetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating regular player position for set {RegularSetId}, user {UserId}", regularSetId, userId);
            return null;
        }
    }

    public async Task<RegularSetDetailedResponse?> UpdatePlayerTeamAsync(int regularSetId, string userId, int team)
    {
        try
        {
            var regular = await _context.Regulars!
                .FirstOrDefaultAsync(r => r.RegularSetId == regularSetId && r.UserId == userId);

            if (regular == null)
                return null;

            regular.TeamAssignment = team;
            await _context.SaveChangesAsync();

            // Fetch and return updated regular set details
            return await GetRegularSetAsync(regularSetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating regular player team for set {RegularSetId}, user {UserId}", regularSetId, userId);
            return null;
        }
    }

    public async Task<(bool Success, string Message)> DeleteRegularSetAsync(int regularSetId)
    {
        try
        {
            // Check if RegularSetId is used in any Sessions
            var hasActiveSessions = await _context.Sessions!.AnyAsync(s => s.RegularSetId == regularSetId);
            if (hasActiveSessions)
            {
                return (false, "Cannot delete regular set as it is being used by one or more sessions");
            }

            // Delete associated regulars first
            var regularsToDelete = await _context.Regulars!.Where(r => r.RegularSetId == regularSetId).ToListAsync();

            _context.Regulars!.RemoveRange(regularsToDelete);

            // Delete the regular set
            var regularSet = await _context.RegularSets!.FirstOrDefaultAsync(rs => rs.RegularSetId == regularSetId);
            if (regularSet == null)
            {
                return (false, "Regular set not found");
            }

            _context.RegularSets!.Remove(regularSet);
            await _context.SaveChangesAsync();

            return (true, "Regular set deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting regular set {RegularSetId}", regularSetId);
            return (false, $"Error deleting regular set: {ex.Message}");
        }
    }

    public async Task<RegularSetDetailedResponse?> AddPlayerAsync(int regularSetId, string userId, int teamAssignment, int positionPreference)
    {
        try
        {
            var regular = new Regular
            {
                RegularSetId = regularSetId,
                UserId = userId,
                TeamAssignment = teamAssignment,
                PositionPreference = positionPreference
            };

            await _context.Regulars!.AddAsync(regular);
            await _context.SaveChangesAsync();

            return await GetRegularSetAsync(regularSetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding regular player to set {RegularSetId}, user {UserId}", regularSetId, userId);
            return null;
        }
    }

    public async Task<RegularSetDetailedResponse?> RemovePlayerAsync(int regularSetId, string userId)
    {
        try
        {
            var regular = await _context.Regulars!
                .FirstOrDefaultAsync(r => r.RegularSetId == regularSetId && r.UserId == userId);

            if (regular == null)
                return null;

            _context.Regulars!.Remove(regular);
            await _context.SaveChangesAsync();

            return await GetRegularSetAsync(regularSetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing regular player from set {RegularSetId}, user {UserId}", regularSetId, userId);
            return null;
        }
    }

    public async Task<RegularSetDetailedResponse?> CreateRegularSetAsync(string description, int dayOfWeek)
    {
        try
        {
            var regularSet = new RegularSet
            {
                Description = description,
                DayOfWeek = dayOfWeek,
                CreateDateTime = DateTime.UtcNow,
                Archived = false
            };

            await _context.RegularSets!.AddAsync(regularSet);
            await _context.SaveChangesAsync();

            return await GetRegularSetAsync(regularSet.RegularSetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating regular set");
            return null;
        }
    }
}

public static class UserMappingExtensions
{
    public static UserDetailedResponse ToDetailedResponse(this AspNetUser user)
    {
        if (user == null) return null;

        return new UserDetailedResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Preferred = user.Preferred,
            PreferredPlus = user.PreferredPlus,
            Active = user.Active,
            Rating = user.Rating,
            LockerRoom13 = user.LockerRoom13,
            EmergencyName = user.EmergencyName,
            EmergencyPhone = user.EmergencyPhone,
            JerseyNumber = user.JerseyNumber,
            MobileLast4 = user.MobileLast4,
            VenmoAccount = user.VenmoAccount,
            PayPalEmail = user.PayPalEmail,
            NotificationPreference = (NotificationPreference) user.NotificationPreference,
            PositionPreference = (PositionPreference) user.PositionPreference,
            PhotoUrl = user.PhotoUrl,
            DateCreated = user.DateCreated,
            Roles = []
        };
    }
}
