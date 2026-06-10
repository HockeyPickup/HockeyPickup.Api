using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HockeyPickup.Api.Helpers;

// Authorizes service-to-service calls (e.g. the Comms daily timer) via a shared secret in a custom header,
// validated against the LotteryServiceApiKey environment variable. Used for the execute-due safety-net endpoint.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ServiceKeyAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Service-Key";
    public const string EnvVarName = "LotteryServiceApiKey";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expectedKey = Environment.GetEnvironmentVariable(EnvVarName);
        if (string.IsNullOrEmpty(expectedKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var providedKey = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey) || providedKey != expectedKey)
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
