using HockeyPickup.Api.Data.Entities;

namespace HockeyPickup.Api.Models.Domain;

// Body of a scheduled lottery-draw Service Bus message, consumed by the API itself.
public class LotteryDrawMessage
{
    public int SessionId { get; set; }
    public LotteryClass LotteryClass { get; set; }
    public DateTime ExpectedDrawDateTimePacific { get; set; }
}
