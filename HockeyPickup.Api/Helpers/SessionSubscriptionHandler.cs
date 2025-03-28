using System.Diagnostics.CodeAnalysis;

namespace HockeyPickup.Api.Helpers;

[ExcludeFromCodeCoverage]
public class SessionSubscriptionHandler : BaseSubscriptionHandler
{
    public SessionSubscriptionHandler(IWebSocketService webSocketService) : base(webSocketService) { }

    public override string OperationType => "SessionUpdated";

    protected override object WrapData(object data) =>
        new { data = new { SessionUpdated = data } };
}
