using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HockeyPickup.Api.Helpers;

public interface ISubscriptionHandler
{
    string OperationType { get; }
    Task HandleSubscription(string socketId, string id);
    Task HandleUpdate(object data);
    Task Cleanup(string socketId);
}

public class WebSocketConnection
{
    public WebSocket Socket { get; set; }
    public DateTime LastActivity { get; set; }
    public string AuthToken { get; set; }
    public HashSet<string> Subscriptions { get; } = new();

    public WebSocketConnection(WebSocket socket, string authToken)
    {
        Socket = socket;
        AuthToken = authToken;
        LastActivity = DateTime.UtcNow;
    }

    public void UpdateActivity()
    {
        LastActivity = DateTime.UtcNow;
    }
}

public class GraphQLSubscription
{
    public Dictionary<string, object>? Extensions { get; set; }
    public string? OperationName { get; set; }
    public string? Query { get; set; }
}

public class WebSocketMessage
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public JsonElement? Payload { get; set; }
}

public class ConnectionInitMessage
{
    public string Type { get; set; } = string.Empty;
    public AuthPayload Payload { get; set; } = new();
}

public class AuthPayload
{
    public string Authorization { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IEnumerable<ISubscriptionHandler> _subscriptionHandlers;
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections;

    public WebSocketMiddleware(RequestDelegate next, IEnumerable<ISubscriptionHandler> subscriptionHandlers, ConcurrentDictionary<string, WebSocketConnection> connections)
    {
        _next = next;
        _subscriptionHandlers = subscriptionHandlers;
        _connections = connections;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var socketId = Guid.NewGuid().ToString();
        var connection = new WebSocketConnection(webSocket, string.Empty);
        _connections.TryAdd(socketId, connection);

        try
        {
            await HandleWebSocketConnection(socketId, connection);
        }
        finally
        {
            _connections.TryRemove(socketId, out _);
            await CleanupSocketSubscriptions(socketId);
        }
    }

    private async Task HandleWebSocketConnection(string socketId, WebSocketConnection connection)
    {
        var buffer = new byte[1024 * 4];

        try
        {
            while (connection.Socket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await connection.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    connection.UpdateActivity();

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Raw message: {message}");
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            AllowTrailingCommas = true
                        };

                        try
                        {
                            if (message.Contains("connection_init"))
                            {
                                var initMessage = JsonSerializer.Deserialize<ConnectionInitMessage>(message, options);
                                connection.AuthToken = initMessage?.Payload.Authorization ?? string.Empty;
                                await SendWebSocketMessage(connection.Socket, new { type = "connection_ack" });
                            }
                            else
                            {
                                var wsMessage = JsonSerializer.Deserialize<WebSocketMessage>(message, options);

                                switch (wsMessage?.Type)
                                {
                                    case "subscribe":
                                        if (wsMessage.Payload.HasValue)
                                        {
                                            var subscription = JsonSerializer.Deserialize<GraphQLSubscription>(
                                                wsMessage.Payload.Value.GetRawText(), options);

                                            if (!string.IsNullOrEmpty(subscription?.OperationName))
                                            {
                                                var handler = _subscriptionHandlers.FirstOrDefault(
                                                    h => h.OperationType == subscription.OperationName);
                                                if (handler != null)
                                                {
                                                    await handler.HandleSubscription(socketId, wsMessage.Id);
                                                    connection.Subscriptions.Add(subscription.OperationName);
                                                }
                                            }
                                        }
                                        break;

                                    case "complete":
                                        foreach (var subscription in connection.Subscriptions)
                                        {
                                            var handler = _subscriptionHandlers.FirstOrDefault(
                                                h => h.OperationType == subscription);
                                            if (handler != null)
                                            {
                                                await handler.Cleanup(socketId);
                                            }
                                        }
                                        connection.Subscriptions.Clear();
                                        break;
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"JSON parsing error: {ex.Message}");
                        }
                    }
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    Console.WriteLine($"Client disconnected abruptly: {socketId}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling WebSocket message: {ex.Message}");
                    break;
                }
            }
        }
        finally
        {
            await CleanupSocketSubscriptions(socketId);
        }
    }

    private async Task SendWebSocketMessage(WebSocket webSocket, object message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task CleanupSocketSubscriptions(string socketId)
    {
        if (_connections.TryGetValue(socketId, out var connection))
        {
            foreach (var subscription in connection.Subscriptions)
            {
                var handler = _subscriptionHandlers.FirstOrDefault(h => h.OperationType == subscription);
                if (handler != null)
                {
                    await handler.Cleanup(socketId);
                }
            }
        }
    }
}
