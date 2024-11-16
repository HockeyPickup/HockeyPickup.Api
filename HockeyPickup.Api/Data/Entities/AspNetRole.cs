using Microsoft.AspNetCore.Identity;
using System.Diagnostics.CodeAnalysis;

namespace HockeyPickup.Api.Data.Entities;

[ExcludeFromCodeCoverage]
public partial class AspNetRole : IdentityRole<string>
{
    public AspNetRole()
    {
        Id = Guid.NewGuid().ToString();
    }

    public virtual ICollection<AspNetUser> Users { get; set; } = new List<AspNetUser>();
}
