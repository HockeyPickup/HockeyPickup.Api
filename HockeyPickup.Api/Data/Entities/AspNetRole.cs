using Microsoft.AspNetCore.Identity;

namespace HockeyPickup.Api.Data.Entities;

public partial class AspNetRole : IdentityRole<string>
{
    public AspNetRole()
    {
        Id = Guid.NewGuid().ToString();
    }

    public virtual ICollection<AspNetUser> Users { get; set; } = new List<AspNetUser>();
}