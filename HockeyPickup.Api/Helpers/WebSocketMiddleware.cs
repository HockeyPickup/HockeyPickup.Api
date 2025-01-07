using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Helpers;

[ExcludeFromCodeCoverage]
public class WebSocketMessage
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public JsonElement? Payload { get; set; }
}

[ExcludeFromCodeCoverage]
public class WebSocketCommand
{
    public string? Type { get; set; }
    public int? SessionId { get; set; }
}

[ExcludeFromCodeCoverage]
public class Variables
{
    [JsonPropertyName("SessionId")]
    public int? SessionId { get; set; }
}

[ExcludeFromCodeCoverage]
public class GraphQLSubscription
{
    public Variables? Variables { get; set; }
    public Dictionary<string, object>? Extensions { get; set; }
    public string? OperationName { get; set; }
    public string? Query { get; set; }
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

[ExcludeFromCodeCoverage]
public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private static readonly ConcurrentDictionary<int, HashSet<string>> _sessionSubscriptions = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _subscriptionsByOperation = new();
    private readonly ConcurrentDictionary<string, string> _socketOperations = new();
    private static readonly ConcurrentDictionary<int, string> _sessionSubscriptionIds = new();

    public WebSocketMiddleware(RequestDelegate next)
    {
        _next = next;
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
        _sockets.TryAdd(socketId, webSocket);

        try
        {
            await HandleWebSocketConnection(socketId, webSocket);
        }
        finally
        {
            _sockets.TryRemove(socketId, out _);
            await CleanupSocketSubscriptions(socketId);
        }
    }

    private async Task HandleWebSocketConnection(string socketId, WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Raw message: {message}");

                        try
                        {
                            if (message.Contains("connection_init"))
                            {
                                var initMessage = JsonSerializer.Deserialize<ConnectionInitMessage>(message);
                                Console.WriteLine($"Connection init received with auth: {initMessage?.Payload.Authorization != null}");
                                await SendWebSocketMessage(webSocket, new { type = "connection_ack" });
                            }
                            else
                            {
                                var options = new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true,
                                    AllowTrailingCommas = true
                                };

                                Console.WriteLine($"Attempting to deserialize message: {message}");
                                var wsMessage = JsonSerializer.Deserialize<WebSocketMessage>(message, options);
                                Console.WriteLine($"Deserialized message - Type: {wsMessage?.Type}, Id: {wsMessage?.Id}, Has Payload: {wsMessage?.Payload.HasValue}");

                                switch (wsMessage?.Type)
                                {
                                    case "connection_init":
                                        await SendWebSocketMessage(webSocket, new { type = "connection_ack" });
                                        break;

                                    case "subscribe":
                                        if (wsMessage.Payload.HasValue)
                                        {
                                            var payloadJson = wsMessage.Payload.Value.GetRawText();
                                            Console.WriteLine($"Subscribe payload: {payloadJson}");

                                            try
                                            {
                                                var subscription = JsonSerializer.Deserialize<GraphQLSubscription>(payloadJson, options);
                                                Console.WriteLine($"Operation: {subscription?.OperationName}");
                                                if (!string.IsNullOrEmpty(subscription?.OperationName))
                                                {
                                                    _socketOperations.TryAdd(socketId, subscription.OperationName);
                                                    _subscriptionsByOperation.AddOrUpdate(
                                                        subscription.OperationName,
                                                        new HashSet<string> { socketId },
                                                        (_, existing) =>
                                                        {
                                                            existing.Add(socketId);
                                                            return existing;
                                                        });

                                                    Console.WriteLine($"Successfully subscribed to {subscription.OperationName} with SessionId {subscription?.Variables?.SessionId}");

                                                    // If this is a session subscription, store it in _sessionSubscriptions too
                                                    if (subscription.Variables?.SessionId.HasValue == true)
                                                    {
                                                        _sessionSubscriptionIds.TryAdd(subscription.Variables.SessionId.Value, wsMessage.Id);
                                                        _sessionSubscriptions.AddOrUpdate(
                                                            subscription.Variables.SessionId.Value,
                                                            new HashSet<string> { socketId },
                                                            (_, existing) =>
                                                            {
                                                                existing.Add(socketId);
                                                                return existing;
                                                            });

                                                        Console.WriteLine($"Added socket {socketId} to session {subscription.Variables.SessionId.Value}");
                                                    }
                                                }
                                            }
                                            catch (JsonException ex)
                                            {
                                                Console.WriteLine($"Error deserializing subscription: {ex.Message}");
                                            }
                                        }
                                        break;

                                    case "complete":
                                        if (_socketOperations.TryRemove(socketId, out var operation))
                                        {
                                            await RemoveSubscription(socketId, operation);
                                        }
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
        if (_socketOperations.TryRemove(socketId, out var operation))
        {
            await RemoveSubscription(socketId, operation);
        }

        // Also clean up session subscriptions
        foreach (var sessionId in _sessionSubscriptions.Keys)
        {
            if (_sessionSubscriptions.TryGetValue(sessionId, out var sockets))
            {
                sockets.Remove(socketId);
                if (!sockets.Any())
                {
                    _sessionSubscriptions.TryRemove(sessionId, out _);
                }
            }
        }
    }

    private Task RemoveSubscription(string socketId, string operation)
    {
        if (_subscriptionsByOperation.TryGetValue(operation, out var sockets))
        {
            sockets.Remove(socketId);
            if (!sockets.Any())
            {
                _subscriptionsByOperation.TryRemove(operation, out _);
            }
        }

        return Task.CompletedTask;
    }

    public async Task BroadcastToOperation(string operationName, object data)
    {
        if (_subscriptionsByOperation.TryGetValue(operationName, out var socketIds))
        {
            var message = new
            {
                type = "next",
                payload = data
            };

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            foreach (var socketId in socketIds)
            {
                if (_sockets.TryGetValue(socketId, out var socket) && socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        // Socket may have closed between check and send
                    }
                }
            }
        }
    }

    public static async Task NotifySessionUpdate(int sessionId, object data)
    {
        if (!_sessionSubscriptions.TryGetValue(sessionId, out var socketIds))
            return;

        // Get the subscription ID
        if (!_sessionSubscriptionIds.TryGetValue(sessionId, out var subscriptionId))
            return;

        var message = JsonSerializer.Serialize(new
        {
            type = "next",
            id = subscriptionId,  // Include the original subscription ID
            payload = new
            {
                data = new
                {
                    SessionUpdated = data
                }
            }
        });

        var bytes = Encoding.UTF8.GetBytes(message);

        foreach (var socketId in socketIds)
        {
            if (_sockets.TryGetValue(socketId, out var socket) && socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"Sent update to socket {socketId} with subscription ID {subscriptionId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending to socket {socketId}: {ex.Message}");
                }
            }
        }
    }
}
