using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HockeyPickup.Api.Data.Entities;
using System.Diagnostics.CodeAnalysis;

namespace HockeyPickup.Api.Data.Context;

[ExcludeFromCodeCoverage]
public class CustomUserStore : UserStore<AspNetUser, AspNetRole, HockeyPickupContext, string>
{
    private readonly HockeyPickupContext _context;

    public CustomUserStore(HockeyPickupContext context, IdentityErrorDescriber? describer = null) : base(context, describer)
    {
        _context = context;
    }

    public override async Task<IdentityResult> CreateAsync(AspNetUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        _context.Set<AspNetUser>().Add(user);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "SaveError",
                Description = e.Message
            });
        }

        return IdentityResult.Success;
    }

    public override async Task<IdentityResult> UpdateAsync(AspNetUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        _context.Set<AspNetUser>().Update(user);
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        try
        {
            // Use raw SQL to update without OUTPUT clause
            var sql = @"UPDATE AspNetUsers SET 
                    UserName = @UserName,
                    NormalizedUserName = @NormalizedUserName,
                    Email = @Email,
                    NormalizedEmail = @NormalizedEmail,
                    EmailConfirmed = @EmailConfirmed,
                    PasswordHash = @PasswordHash,
                    SecurityStamp = @SecurityStamp,
                    ConcurrencyStamp = @ConcurrencyStamp,
                    PhoneNumber = @PhoneNumber,
                    PhoneNumberConfirmed = @PhoneNumberConfirmed,
                    TwoFactorEnabled = @TwoFactorEnabled,
                    LockoutEndDateUtc = @LockoutEndDateUtc,
                    LockoutEnabled = @LockoutEnabled,
                    AccessFailedCount = @AccessFailedCount,
                    FirstName = @FirstName,
                    LastName = @LastName,
                    NotificationPreference = @NotificationPreference,
                    PositionPreference = @PositionPreference,
                    Active = @Active,
                    Preferred = @Preferred,
                    Rating = @Rating,
                    PreferredPlus = @PreferredPlus,
                    EmergencyName = @EmergencyName,
                    EmergencyPhone = @EmergencyPhone,
                    JerseyNumber = @JerseyNumber,
                    LockerRoom13 = @LockerRoom13,
                    DateCreated = @DateCreated,
                    PhotoUrl = @PhotoUrl
                    WHERE Id = @Id";

            await _context.Database.ExecuteSqlRawAsync(sql,
                new Microsoft.Data.SqlClient.SqlParameter("@UserName", user.UserName ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@NormalizedUserName", user.NormalizedUserName ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@Email", user.Email ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@NormalizedEmail", user.NormalizedEmail ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@EmailConfirmed", user.EmailConfirmed),
                new Microsoft.Data.SqlClient.SqlParameter("@PasswordHash", user.PasswordHash ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@SecurityStamp", user.SecurityStamp ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@ConcurrencyStamp", user.ConcurrencyStamp ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@PhoneNumber", user.PhoneNumber ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@PhoneNumberConfirmed", user.PhoneNumberConfirmed),
                new Microsoft.Data.SqlClient.SqlParameter("@TwoFactorEnabled", user.TwoFactorEnabled),
                new Microsoft.Data.SqlClient.SqlParameter("@LockoutEndDateUtc", user.LockoutEndDateUtc ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@LockoutEnabled", user.LockoutEnabled),
                new Microsoft.Data.SqlClient.SqlParameter("@AccessFailedCount", user.AccessFailedCount),
                new Microsoft.Data.SqlClient.SqlParameter("@FirstName", user.FirstName ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@LastName", user.LastName ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@NotificationPreference", user.NotificationPreference),
                new Microsoft.Data.SqlClient.SqlParameter("@PositionPreference", user.PositionPreference),
                new Microsoft.Data.SqlClient.SqlParameter("@Active", user.Active),
                new Microsoft.Data.SqlClient.SqlParameter("@Preferred", user.Preferred),
                new Microsoft.Data.SqlClient.SqlParameter("@Rating", user.Rating),
                new Microsoft.Data.SqlClient.SqlParameter("@PreferredPlus", user.PreferredPlus),
                new Microsoft.Data.SqlClient.SqlParameter("@EmergencyName", user.EmergencyName ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@EmergencyPhone", user.EmergencyPhone ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@JerseyNumber", user.JerseyNumber),
                new Microsoft.Data.SqlClient.SqlParameter("@LockerRoom13", user.LockerRoom13),
                new Microsoft.Data.SqlClient.SqlParameter("@DateCreated", user.DateCreated),
                new Microsoft.Data.SqlClient.SqlParameter("@PhotoUrl", user.PhotoUrl ?? (object) DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@Id", user.Id));

            return IdentityResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "ConcurrencyFailure",
                Description = "Optimistic concurrency failure, object has been modified."
            });
        }
    }
}
