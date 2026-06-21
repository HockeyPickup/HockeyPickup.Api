using HockeyPickup.Api.Models.Responses;

namespace HockeyPickup.Api.Helpers;

public class SessionSubscriptionHandler : BaseSubscriptionHandler
{
    public SessionSubscriptionHandler(IWebSocketService webSocketService) : base(webSocketService) { }

    public override string OperationType => "SessionUpdated";

    protected override object WrapData(object data) =>
        new { data = new { SessionUpdated = data } };

    // Updates carry the full session; deletes carry just the SessionId. Match on the SessionId
    // so only sockets subscribed to that session receive the push.
    protected override string? GetEntityId(object data) => data switch
    {
        SessionDetailedResponse session => session.SessionId.ToString(),
        int sessionId => sessionId.ToString(),
        _ => null
    };
}
