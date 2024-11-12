using System.Threading.Channels;

namespace HockeyPickup.Api.Services;

public interface IServiceBus
{
    Task SendAsync<T>(T message, string? subject = null, string? correlationId = null, string? queueName = null, CancellationToken cancellationToken = default) where T : class;
    Channel<FailedMessage> GetRetryChannel();
}
