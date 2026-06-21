using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HockeyPickup.Api.Helpers;

// Authorizes service-to-service calls (e.g. the Comms daily timer) via a shared secret in a custom header.
// The expected key resolves from configuration (LotteryServiceApiKey in appsettings) with an environment-variable
// fallback, mirroring how ResilientServiceBus resolves its connection string. Used for the execute-due endpoint.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ServiceKeyAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Service-Key";
    public const string ConfigKeyName = "LotteryServiceApiKey";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices?.GetService<IConfiguration>();
        var expectedKey = configuration?[ConfigKeyName] ?? Environment.GetEnvironmentVariable(ConfigKeyName);
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
