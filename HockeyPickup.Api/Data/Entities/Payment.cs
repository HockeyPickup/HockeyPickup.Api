using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HockeyPickup.Api.Data.Entities;

// Entity for storing payment method details
public class UserPaymentMethod
{
    [Key]
    public int UserPaymentMethodId { get; set; }

    [Required]
    [MaxLength(128)]
    public string UserId { get; set; } = null!;

    [Required]
    public PaymentMethodType MethodType { get; set; }

    [Required]
    [MaxLength(256)]
    public string Identifier { get; set; } = null!;  // Email, username, wallet address etc.

    [Required]
    public int PreferenceOrder { get; set; }  // Lower number = higher preference

    [Required]
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    [ForeignKey("UserId")]
    public virtual AspNetUser User { get; set; } = null!;
}

// Add to AspNetUser class
public partial class AspNetUser
{
    // Existing properties...

    public virtual ICollection<UserPaymentMethod> PaymentMethods { get; set; }
        = new List<UserPaymentMethod>();
}
