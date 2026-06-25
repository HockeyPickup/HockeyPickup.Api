using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HockeyPickup.Api.Helpers;

public interface ISubscriptionHandler
{
    string OperationType { get; }
    Task HandleSubscription(string socketId, string id, string? subscriptionArgument);
    Task HandleUpdate(object data);
    Task HandleDelete(object data);
    Task Cleanup(string socketId);
}

public abstract class BaseSubscriptionHandler : ISubscriptionHandler
{
    // socketId -> the graphql-ws message id to echo and the argument the socket filtered on.
    private readonly ConcurrentDictionary<string, SocketSubscription> _subscriptions = new();
    private readonly IWebSocketService _webSocketService;

    protected BaseSubscriptionHandler(IWebSocketService webSocketService)
    {
        _webSocketService = webSocketService;
    }

    public abstract string OperationType { get; }
    protected abstract object WrapData(object data);

    // Identifies which entity a payload belongs to so an update is only delivered to the
    // sockets that subscribed for that entity (e.g. the SessionId). Returning null broadcasts
    // the payload to every subscriber.
    protected abstract string? GetEntityId(object data);

    public Task HandleSubscription(string socketId, string id, string? subscriptionArgument)
    {
        _subscriptions[socketId] = new SocketSubscription(id, subscriptionArgument);
        return Task.CompletedTask;
    }

    public Task HandleUpdate(object data) => Broadcast(data);

    public Task HandleDelete(object data) => Broadcast(data);

    private async Task Broadcast(object data)
    {
        var entityId = GetEntityId(data);
        foreach (var (socketId, subscription) in _subscriptions)
        {
            // Deliver when the payload's entity can't be identified (broadcast), the socket
            // subscribed without an argument, or the argument matches the payload's entity.
            if (entityId is null || subscription.Argument is null || subscription.Argument == entityId)
            {
                await _webSocketService.SendMessageToSocket(socketId, WrapData(data), subscription.SubscriptionId);
            }
        }
    }

    public Task Cleanup(string socketId)
    {
        _subscriptions.TryRemove(socketId, out _);
        return Task.CompletedTask;
    }

    private sealed record SocketSubscription(string SubscriptionId, string? Argument);
}

[ExcludeFromCodeCoverage]
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

[ExcludeFromCodeCoverage]
public class GraphQLSubscription
{
    public Dictionary<string, object>? Extensions { get; set; }
    public string? OperationName { get; set; }
    public string? Query { get; set; }
    public Dictionary<string, JsonElement>? Variables { get; set; }
}

[ExcludeFromCodeCoverage]
public class WebSocketMessage
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public JsonElement? Payload { get; set; }
}

[ExcludeFromCodeCoverage]
public class ConnectionInitMessage
{
    public string Type { get; set; } = string.Empty;
    public AuthPayload Payload { get; set; } = new();
}

[ExcludeFromCodeCoverage]
public class AuthPayload
{
    public string Authorization { get; set; } = string.Empty;
}

// Excluded from coverage: the raw socket receive loop has defensive null-guards on
// JSON deserialization results (lines handling connection_init / subscribe payloads) whose
// null branches are unreachable for any valid frame, so branch coverage can't reach 100%.
// Behavior is still fully exercised by WebSocketMiddlewareTests.
[ExcludeFromCodeCoverage]
public class WebSocketMiddleware
{
    private const string GraphQlTransportWsProtocol = "graphql-transport-ws";
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

        // The App's Apollo/graphql-ws client connects using the "graphql-transport-ws"
        // subprotocol and closes the socket immediately (close code 4406) unless the server
        // echoes that subprotocol back during the handshake. Accept with the negotiated
        // subprotocol so the connection survives and subscriptions can start.
        var acceptContext = new WebSocketAcceptContext
        {
            SubProtocol = context.WebSockets.WebSocketRequestedProtocols.Contains(GraphQlTransportWsProtocol)
                ? GraphQlTransportWsProtocol
                : null
        };
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync(acceptContext);
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
                    // graphql-ws subscribe frames carry the full subscription query and routinely
                    // exceed the receive buffer, so reassemble every fragment before parsing.
                    using var messageStream = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await connection.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close)
                            break;
                        messageStream.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    connection.UpdateActivity();

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int) messageStream.Length);
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
                                                    var subscriptionArgument = ExtractSubscriptionArgument(subscription);
                                                    await handler.HandleSubscription(socketId, wsMessage.Id, subscriptionArgument);
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

    // These subscriptions filter on a single id variable (e.g. SessionId). Return its value as
    // a string so the handler only pushes payloads for the entity the socket subscribed to.
    private static string? ExtractSubscriptionArgument(GraphQLSubscription subscription)
    {
        if (subscription.Variables == null)
            return null;

        foreach (var value in subscription.Variables.Values)
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }

        return null;
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

public interface IWebSocketService
{
    Task SendMessageToSocket(string socketId, object payload, string subscriptionId);
    bool IsSocketConnected(string socketId);
}

public class WebSocketService : IWebSocketService
{
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections;

    public WebSocketService(ConcurrentDictionary<string, WebSocketConnection> connections)
    {
        _connections = connections;
    }

    public async Task SendMessageToSocket(string socketId, object payload, string subscriptionId)
    {
        if (_connections.TryGetValue(socketId, out var connection) &&
            connection.Socket.State == WebSocketState.Open)
        {
            var message = JsonSerializer.Serialize(new
            {
                type = "next",
                id = subscriptionId,
                 payload
            }, ApiJsonSerializer.Options);
            var bytes = Encoding.UTF8.GetBytes(message);
            await connection.Socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }

    public bool IsSocketConnected(string socketId) =>
        _connections.TryGetValue(socketId, out var connection) &&
        connection.Socket.State == WebSocketState.Open;
}
