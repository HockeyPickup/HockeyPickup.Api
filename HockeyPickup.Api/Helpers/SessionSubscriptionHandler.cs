using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text;

namespace HockeyPickup.Api.Helpers;

public class SessionSubscriptionHandler : ISubscriptionHandler
{
    private readonly HashSet<string> _subscribedSockets = new();
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections;
    private readonly ConcurrentDictionary<string, string> _subscriptionIds = new();

    public SessionSubscriptionHandler(ConcurrentDictionary<string, WebSocketConnection> connections)
    {
        _connections = connections;
    }

    public string OperationType => "SessionUpdated";

    public Task HandleSubscription(string socketId, string id)
    {
        _subscriptionIds[socketId] = id;
        _subscribedSockets.Add(socketId);
        Console.WriteLine($"Added subscription with ID: {id}");
        return Task.CompletedTask;
    }

    public async Task HandleUpdate(object data)
    {
        foreach (var socketId in _subscribedSockets)
        {
            if (!_subscriptionIds.TryGetValue(socketId, out var subscriptionId) ||
                !_connections.TryGetValue(socketId, out var connection) ||
                connection.Socket.State != WebSocketState.Open)
                continue;

            try
            {
                var message = JsonSerializer.Serialize(new
                {
                    type = "next",
                    id = subscriptionId,  // Use stored subscription ID
                    payload = new { data = new { SessionUpdated = data } }
                });

                var bytes = Encoding.UTF8.GetBytes(message);
                await connection.Socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to socket {socketId}: {ex.Message}");
            }
        }
    }

    public Task Cleanup(string socketId)
    {
        _subscribedSockets.Remove(socketId);
        _subscriptionIds.TryRemove(socketId, out _);
        return Task.CompletedTask;
    }
}
