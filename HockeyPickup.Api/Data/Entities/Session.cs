using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HockeyPickup.Api.Data.Entities;

public class Session
{
    [Key]
    public int SessionId { get; set; }
    public DateTime CreateDateTime { get; set; }
    public DateTime UpdateDateTime { get; set; }
    public string? Note { get; set; }
    public DateTime SessionDate { get; set; }
    public int? RegularSetId { get; set; }
    public int? BuyDayMinimum { get; set; }

    // Navigation properties
    [ForeignKey("RegularSetId")]
    public virtual RegularSet? RegularSet { get; set; }
    public virtual ICollection<BuySell> BuySells { get; set; } = new List<BuySell>();
    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public virtual ICollection<RosterPlayer> CurrentRosters { get; set; } = new List<RosterPlayer>();
    public virtual ICollection<BuyingQueue> BuyingQueues { get; set; } = new List<BuyingQueue>();
}

public class RegularSet
{
    [Key]
    public int RegularSetId { get; set; }
    public string? Description { get; set; }
    public int DayOfWeek { get; set; }
    public DateTime CreateDateTime { get; set; }

    // Navigation properties
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
    public virtual ICollection<Regular> Regulars { get; set; } = new List<Regular>();
}

public class Regular
{
    [Key]
    [Column(Order = 0)]
    public int RegularSetId { get; set; }

    [Key]
    [Column(Order = 1)]
    public string? UserId { get; set; }

    public int TeamAssignment { get; set; }
    public int PositionPreference { get; set; }

    // Navigation properties
    [ForeignKey("RegularSetId")]
    public virtual RegularSet? RegularSet { get; set; }

    [ForeignKey("UserId")]
    public virtual AspNetUser? User { get; set; }
}

public class BuySell
{
    [Key]
    public int BuySellId { get; set; }
    public int SessionId { get; set; }
    public string? BuyerUserId { get; set; }
    public string? SellerUserId { get; set; }
    public string? SellerNote { get; set; }
    public string? BuyerNote { get; set; }
    public bool PaymentSent { get; set; }
    public bool PaymentReceived { get; set; }
    public DateTime CreateDateTime { get; set; }
    public DateTime UpdateDateTime { get; set; }
    public int TeamAssignment { get; set; }
    public bool SellerNoteFlagged { get; set; }
    public bool BuyerNoteFlagged { get; set; }

    // Navigation properties
    [ForeignKey("SessionId")]
    public virtual Session? Session { get; set; }

    [ForeignKey("BuyerUserId")]
    public virtual AspNetUser? Buyer { get; set; }

    [ForeignKey("SellerUserId")]
    public virtual AspNetUser? Seller { get; set; }
}

public class ActivityLog
{
    [Key]
    public int ActivityLogId { get; set; }
    public int SessionId { get; set; }
    public string? UserId { get; set; }
    public DateTime CreateDateTime { get; set; }
    public string? Activity { get; set; }

    // Navigation properties
    [ForeignKey("SessionId")]
    public virtual Session? Session { get; set; }

    [ForeignKey("UserId")]
    public virtual AspNetUser? User { get; set; }
}

public class SessionRoster
{
    [Key]
    public int SessionRosterId { get; set; }
    public int SessionId { get; set; }
    public string UserId { get; set; } = null!;
    public int TeamAssignment { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsRegular { get; set; }
    public DateTime JoinedDateTime { get; set; }
    public DateTime? LeftDateTime { get; set; }
    public int? LastBuySellId { get; set; }
    public int Position { get; set; }

    // Navigation properties
    [ForeignKey("SessionId")]
    public virtual Session? Session { get; set; }

    [ForeignKey("UserId")]
    public virtual AspNetUser? User { get; set; }
}

[Table("CurrentSessionRoster")]
public class RosterPlayer
{
    [Key]
    public int SessionRosterId { get; set; }
    public string UserId { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public int SessionId { get; set; }
    public int TeamAssignment { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsRegular { get; set; }
    public string PlayerStatus { get; set; } = null!;
    public decimal Rating { get; set; }
    public bool Preferred { get; set; }
    public bool PreferredPlus { get; set; }
    public int? LastBuySellId { get; set; }
    public DateTime JoinedDateTime { get; set; }
    public int Position { get; set; }
    public string CurrentPosition { get; set; } = null!;
}

[Table("SessionBuyingQueue")]
public class BuyingQueue
{
    [Key]
    public int BuySellId { get; set; }
    public int SessionId { get; set; }
    public string? BuyerName { get; set; }
    public string? SellerName { get; set; }
    public int TeamAssignment { get; set; }
    public string TransactionStatus { get; set; } = null!;
    public string QueueStatus { get; set; } = null!;
    public bool PaymentSent { get; set; }
    public bool PaymentReceived { get; set; }
    public string? BuyerNote { get; set; }
    public string? SellerNote { get; set; }
}

