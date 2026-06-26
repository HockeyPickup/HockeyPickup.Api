using FluentAssertions;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HockeyPickup.Api.Tests.HelpersTests;

// A hand-written WebSocket test double. ReceiveAsync replays a queue of scripted frames (each
// frame may write into the caller's buffer or throw), and SendAsync records what the server
// wrote. Once the queue drains, State flips to Closed so the middleware's receive loop exits.
internal sealed class FakeWebSocket : WebSocket
{
    private readonly Queue<Func<ArraySegment<byte>, WebSocketReceiveResult>> _receives;
    private WebSocketState _state;

    public List<string> SentMessages { get; } = new();

    public FakeWebSocket(
        IEnumerable<Func<ArraySegment<byte>, WebSocketReceiveResult>>? receives = null,
        WebSocketState initialState = WebSocketState.Open)
    {
        _receives = new Queue<Func<ArraySegment<byte>, WebSocketReceiveResult>>(
            receives ?? Enumerable.Empty<Func<ArraySegment<byte>, WebSocketReceiveResult>>());
        _state = initialState;
    }

    public override WebSocketState State => _state;

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        if (_receives.Count == 0)
        {
            _state = WebSocketState.Closed;
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }

        var next = _receives.Dequeue();
        var result = next(buffer);
        if (_receives.Count == 0)
            _state = WebSocketState.Closed;
        return Task.FromResult(result);
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        SentMessages.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
        return Task.CompletedTask;
    }

    public static Func<ArraySegment<byte>, WebSocketReceiveResult> Text(string message) => buffer =>
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        Array.Copy(bytes, 0, buffer.Array!, buffer.Offset, bytes.Length);
        return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true);
    };

    public static Func<ArraySegment<byte>, WebSocketReceiveResult> TextFragment(byte[] chunk, bool endOfMessage) => buffer =>
    {
        Array.Copy(chunk, 0, buffer.Array!, buffer.Offset, chunk.Length);
        return new WebSocketReceiveResult(chunk.Length, WebSocketMessageType.Text, endOfMessage);
    };

    public static Func<ArraySegment<byte>, WebSocketReceiveResult> Binary() =>
        _ => new WebSocketReceiveResult(0, WebSocketMessageType.Binary, true);

    public static Func<ArraySegment<byte>, WebSocketReceiveResult> Throws(Exception ex) =>
        _ => throw ex;

    public override void Abort() { }
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
    public override void Dispose() { }
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override string? SubProtocol => null;
}

public class SessionSubscriptionHandlerTests
{
    private static SessionDetailedResponse Session(int sessionId) => new()
    {
        SessionId = sessionId,
        CreateDateTime = DateTime.UtcNow,
        UpdateDateTime = DateTime.UtcNow,
        SessionDate = DateTime.UtcNow.Date,
        Note = string.Empty
    };

    [Fact]
    public async Task HandleUpdate_OnlyDeliversToSocketsSubscribedToThatSession()
    {
        // Arrange - two sockets watching different sessions
        var ws = new Mock<IWebSocketService>();
        var handler = new SessionSubscriptionHandler(ws.Object);
        await handler.HandleSubscription("sockA", "subA", "100");
        await handler.HandleSubscription("sockB", "subB", "200");

        // Act - session 100 changes
        await handler.HandleUpdate(Session(100));

        // Assert - only the socket watching session 100 is notified
        ws.Verify(s => s.SendMessageToSocket("sockA", It.IsAny<object>(), "subA"), Times.Once);
        ws.Verify(s => s.SendMessageToSocket("sockB", It.IsAny<object>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleUpdate_WhenEntityIdUnknown_BroadcastsToAllSubscribers()
    {
        // Arrange - payload type the handler cannot map to a SessionId
        var ws = new Mock<IWebSocketService>();
        var handler = new SessionSubscriptionHandler(ws.Object);
        await handler.HandleSubscription("sockA", "subA", "100");
        await handler.HandleSubscription("sockB", "subB", "200");

        // Act
        await handler.HandleUpdate("unmappable-payload");

        // Assert - falls back to broadcasting
        ws.Verify(s => s.SendMessageToSocket("sockA", It.IsAny<object>(), "subA"), Times.Once);
        ws.Verify(s => s.SendMessageToSocket("sockB", It.IsAny<object>(), "subB"), Times.Once);
    }

    [Fact]
    public async Task HandleUpdate_WhenSubscriberHasNoArgument_StillDelivers()
    {
        // Arrange - socket subscribed without a SessionId filter
        var ws = new Mock<IWebSocketService>();
        var handler = new SessionSubscriptionHandler(ws.Object);
        await handler.HandleSubscription("sockA", "subA", null);

        // Act
        await handler.HandleUpdate(Session(100));

        // Assert
        ws.Verify(s => s.SendMessageToSocket("sockA", It.IsAny<object>(), "subA"), Times.Once);
    }

    [Fact]
    public async Task HandleDelete_MatchesOnSessionIdInteger()
    {
        // Arrange - delete carries just the SessionId as an int
        var ws = new Mock<IWebSocketService>();
        var handler = new SessionSubscriptionHandler(ws.Object);
        await handler.HandleSubscription("sockA", "subA", "100");
        await handler.HandleSubscription("sockB", "subB", "200");

        // Act
        await handler.HandleDelete(100);

        // Assert
        ws.Verify(s => s.SendMessageToSocket("sockA", It.IsAny<object>(), "subA"), Times.Once);
        ws.Verify(s => s.SendMessageToSocket("sockB", It.IsAny<object>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Cleanup_RemovesSubscriptionSoNoFurtherDelivery()
    {
        // Arrange
        var ws = new Mock<IWebSocketService>();
        var handler = new SessionSubscriptionHandler(ws.Object);
        await handler.HandleSubscription("sockA", "subA", "100");

        // Act
        await handler.Cleanup("sockA");
        await handler.HandleUpdate(Session(100));

        // Assert
        ws.Verify(s => s.SendMessageToSocket(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleUpdate_WrapsPayloadAsSessionUpdated()
    {
        // Arrange
        var ws = new Mock<IWebSocketService>();
        object? captured = null;
        ws.Setup(s => s.SendMessageToSocket(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()))
            .Callback<string, object, string>((_, payload, _) => captured = payload);
        var handler = new SessionSubscriptionHandler(ws.Object);
        await handler.HandleSubscription("sockA", "subA", "100");

        // Act
        await handler.HandleUpdate(Session(100));

        // Assert - shape is { data: { SessionUpdated: <session> } }
        handler.OperationType.Should().Be("SessionUpdated");
        var json = JsonSerializer.Serialize(captured);
        json.Should().Contain("SessionUpdated").And.Contain("\"SessionId\":100");
    }
}

public class WebSocketServiceTests
{
    [Fact]
    public async Task SendMessageToSocket_WhenConnectedAndOpen_SendsNextEnvelope()
    {
        // Arrange
        var socket = new FakeWebSocket();
        var connections = new ConcurrentDictionary<string, WebSocketConnection>();
        connections["s1"] = new WebSocketConnection(socket, string.Empty);
        var service = new WebSocketService(connections);

        // Act
        await service.SendMessageToSocket("s1", new { hello = "world" }, "sub1");

        // Assert
        socket.SentMessages.Should().ContainSingle();
        socket.SentMessages[0].Should()
            .Contain("\"type\":\"next\"").And
            .Contain("\"id\":\"sub1\"").And
            .Contain("hello");
    }

    [Fact]
    public async Task SendMessageToSocket_StripsEveryRatingFromThePayload()
    {
        // Arrange - a payload with nested Rating fields (objects + arrays + scalars)
        var socket = new FakeWebSocket();
        var connections = new ConcurrentDictionary<string, WebSocketConnection>();
        connections["s1"] = new WebSocketConnection(socket, string.Empty);
        var service = new WebSocketService(connections);
        var payload = new
        {
            data = new
            {
                SessionUpdated = new
                {
                    Cost = 27m,
                    CurrentRosters = new[]
                    {
                        new { UserId = "u1", Rating = 5.5m },
                        new { UserId = "u2", Rating = 9.1m },
                    },
                },
            },
        };

        // Act
        await service.SendMessageToSocket("s1", payload, "sub1");

        // Assert - ratings zeroed, everything else intact
        var sent = socket.SentMessages.Should().ContainSingle().Subject;
        sent.Should().Contain("\"Rating\":0");
        sent.Should().NotContain("5.5").And.NotContain("9.1");
        sent.Should().Contain("\"Cost\":27").And.Contain("\"UserId\":\"u1\"");
    }

    [Fact]
    public async Task SendMessageToSocket_WhenSocketNotOpen_DoesNotSend()
    {
        // Arrange
        var socket = new FakeWebSocket(initialState: WebSocketState.Closed);
        var connections = new ConcurrentDictionary<string, WebSocketConnection>();
        connections["s1"] = new WebSocketConnection(socket, string.Empty);
        var service = new WebSocketService(connections);

        // Act
        await service.SendMessageToSocket("s1", new { hello = "world" }, "sub1");

        // Assert
        socket.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendMessageToSocket_WhenSocketMissing_DoesNothing()
    {
        // Arrange
        var connections = new ConcurrentDictionary<string, WebSocketConnection>();
        var service = new WebSocketService(connections);

        // Act
        var act = async () => await service.SendMessageToSocket("missing", new { }, "sub1");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void IsSocketConnected_ReflectsConnectionState()
    {
        // Arrange
        var connections = new ConcurrentDictionary<string, WebSocketConnection>();
        connections["open"] = new WebSocketConnection(new FakeWebSocket(), string.Empty);
        connections["closed"] = new WebSocketConnection(new FakeWebSocket(initialState: WebSocketState.Closed), string.Empty);
        var service = new WebSocketService(connections);

        // Act & Assert
        service.IsSocketConnected("open").Should().BeTrue();
        service.IsSocketConnected("closed").Should().BeFalse();
        service.IsSocketConnected("missing").Should().BeFalse();
    }
}

public class WebSocketMiddlewareTests
{
    private const string GraphQlProtocol = "graphql-transport-ws";

    private static Mock<ISubscriptionHandler> Handler(string operationType = "SessionUpdated")
    {
        var handler = new Mock<ISubscriptionHandler>();
        handler.SetupGet(h => h.OperationType).Returns(operationType);
        return handler;
    }

    private static (Mock<HttpContext> context, Mock<WebSocketManager> wsManager) BuildContext(
        FakeWebSocket socket,
        bool isWebSocketRequest = true,
        IList<string>? requestedProtocols = null,
        Action<WebSocketAcceptContext>? onAccept = null)
    {
        var wsManager = new Mock<WebSocketManager>();
        wsManager.SetupGet(m => m.IsWebSocketRequest).Returns(isWebSocketRequest);
        wsManager.SetupGet(m => m.WebSocketRequestedProtocols).Returns(requestedProtocols ?? new List<string>());
        wsManager.Setup(m => m.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()))
            .Callback<WebSocketAcceptContext>(ctx => onAccept?.Invoke(ctx))
            .ReturnsAsync(socket);

        var context = new Mock<HttpContext>();
        context.SetupGet(c => c.WebSockets).Returns(wsManager.Object);
        return (context, wsManager);
    }

    private static WebSocketMiddleware CreateMiddleware(
        IEnumerable<ISubscriptionHandler> handlers,
        ConcurrentDictionary<string, WebSocketConnection> connections,
        RequestDelegate? next = null) =>
        new(next ?? (_ => Task.CompletedTask), handlers, connections);

    [Fact]
    public async Task InvokeAsync_WhenNotAWebSocketRequest_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        var (context, wsManager) = BuildContext(new FakeWebSocket(), isWebSocketRequest: false);
        var middleware = CreateMiddleware(
            Array.Empty<ISubscriptionHandler>(),
            new ConcurrentDictionary<string, WebSocketConnection>(),
            _ => { nextCalled = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        nextCalled.Should().BeTrue();
        wsManager.Verify(m => m.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenClientRequestsGraphQlProtocol_AcceptsWithThatSubProtocol()
    {
        // Arrange
        WebSocketAcceptContext? accepted = null;
        var (context, _) = BuildContext(
            new FakeWebSocket(),
            requestedProtocols: new List<string> { GraphQlProtocol },
            onAccept: ctx => accepted = ctx);
        var connections = new ConcurrentDictionary<string, WebSocketConnection>();
        var middleware = CreateMiddleware(Array.Empty<ISubscriptionHandler>(), connections);

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert - the negotiated subprotocol is echoed so graphql-ws keeps the connection
        accepted.Should().NotBeNull();
        accepted!.SubProtocol.Should().Be(GraphQlProtocol);
        connections.Should().BeEmpty(); // cleaned up when the loop ends
    }

    [Fact]
    public async Task InvokeAsync_WhenClientDoesNotRequestGraphQlProtocol_AcceptsWithoutSubProtocol()
    {
        // Arrange
        WebSocketAcceptContext? accepted = null;
        var (context, _) = BuildContext(
            new FakeWebSocket(),
            requestedProtocols: new List<string>(),
            onAccept: ctx => accepted = ctx);
        var middleware = CreateMiddleware(
            Array.Empty<ISubscriptionHandler>(),
            new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        accepted.Should().NotBeNull();
        accepted!.SubProtocol.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_OnConnectionInit_RepliesWithConnectionAck()
    {
        // Arrange
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"type":"connection_init","payload":{"authorization":"Bearer abc"}}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(
            Array.Empty<ISubscriptionHandler>(),
            new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        socket.SentMessages.Should().ContainSingle(m => m.Contains("connection_ack"));
    }

    [Fact]
    public async Task InvokeAsync_OnSubscribe_RegistersHandlerWithSessionIdArgument()
    {
        // Arrange
        var handler = Handler();
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"id":"sub-1","type":"subscribe","payload":{"operationName":"SessionUpdated","variables":{"SessionId":123}}}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert - the numeric SessionId variable is forwarded as the filter argument
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), "sub-1", "123"), Times.Once);
        // ...and the connection is cleaned up when the loop ends
        handler.Verify(h => h.Cleanup(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ReassemblesFragmentedSubscribeMessage()
    {
        // Arrange - a subscribe message split across two frames, as happens when the (large)
        // subscription query exceeds the receive buffer.
        var handler = Handler();
        var bytes = Encoding.UTF8.GetBytes(
            """{"id":"sub-1","type":"subscribe","payload":{"operationName":"SessionUpdated","variables":{"SessionId":42}}}""");
        var mid = bytes.Length / 2;
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.TextFragment(bytes[..mid], endOfMessage: false),
            FakeWebSocket.TextFragment(bytes[mid..], endOfMessage: true)
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert - the fragments were reassembled and the subscription registered
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), "sub-1", "42"), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_OnSubscribe_WithStringVariable_ForwardsStringArgument()
    {
        // Arrange
        var handler = Handler();
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"id":"sub-1","type":"subscribe","payload":{"operationName":"SessionUpdated","variables":{"SessionId":"abc"}}}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), "sub-1", "abc"), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_OnSubscribe_WithoutVariables_ForwardsNullArgument()
    {
        // Arrange
        var handler = Handler();
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"id":"sub-1","type":"subscribe","payload":{"operationName":"SessionUpdated"}}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), "sub-1", null), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_OnSubscribe_WithEmptyVariables_ForwardsNullArgument()
    {
        // Arrange
        var handler = Handler();
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"id":"sub-1","type":"subscribe","payload":{"operationName":"SessionUpdated","variables":{}}}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), "sub-1", null), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_OnSubscribe_WithUnknownOperation_IsIgnored()
    {
        // Arrange
        var handler = Handler();
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"id":"sub-1","type":"subscribe","payload":{"operationName":"SomethingElse"}}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_OnSubscribe_WithEmptyOperationName_IsIgnored()
    {
        // Arrange
        var handler = Handler();
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"id":"sub-1","type":"subscribe","payload":{"operationName":""}}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_OnSubscribe_WithoutPayload_IsIgnored()
    {
        // Arrange
        var handler = Handler();
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"id":"sub-1","type":"subscribe"}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_OnComplete_CleansUpRegisteredSubscriptions()
    {
        // Arrange - subscribe, then complete
        var handler = Handler();
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("""{"id":"sub-1","type":"subscribe","payload":{"operationName":"SessionUpdated","variables":{"SessionId":1}}}"""),
            FakeWebSocket.Text("""{"id":"sub-1","type":"complete"}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert - cleaned up exactly once via the complete message (subscriptions then cleared)
        handler.Verify(h => h.Cleanup(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_OnInvalidJson_IsCaughtAndProcessingContinues()
    {
        // Arrange - a malformed frame followed by a valid connection_init
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Text("{ this is not valid json"),
            FakeWebSocket.Text("""{"type":"connection_init"}""")
        });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(
            Array.Empty<ISubscriptionHandler>(),
            new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        await middleware.InvokeAsync(context.Object);

        // Assert - the malformed frame did not kill the loop; the ack still went out
        socket.SentMessages.Should().ContainSingle(m => m.Contains("connection_ack"));
    }

    [Fact]
    public async Task InvokeAsync_OnNonTextFrame_IsIgnored()
    {
        // Arrange
        var socket = new FakeWebSocket(new[] { FakeWebSocket.Binary() });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(
            Array.Empty<ISubscriptionHandler>(),
            new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        var act = async () => await middleware.InvokeAsync(context.Object);

        // Assert
        await act.Should().NotThrowAsync();
        socket.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenClientDisconnectsAbruptly_BreaksCleanly()
    {
        // Arrange
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Throws(new WebSocketException(WebSocketError.ConnectionClosedPrematurely))
        });
        var connections = new ConcurrentDictionary<string, WebSocketConnection>();
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(Array.Empty<ISubscriptionHandler>(), connections);

        // Act
        var act = async () => await middleware.InvokeAsync(context.Object);

        // Assert
        await act.Should().NotThrowAsync();
        connections.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenReceiveThrowsUnexpectedly_BreaksCleanly()
    {
        // Arrange
        var socket = new FakeWebSocket(new[]
        {
            FakeWebSocket.Throws(new InvalidOperationException("boom"))
        });
        var connections = new ConcurrentDictionary<string, WebSocketConnection>();
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(Array.Empty<ISubscriptionHandler>(), connections);

        // Act
        var act = async () => await middleware.InvokeAsync(context.Object);

        // Assert
        await act.Should().NotThrowAsync();
        connections.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_OnUnrecognizedMessageType_IsIgnored()
    {
        // Arrange - valid JSON but not a subscribe/complete (exercises the switch default)
        var handler = Handler();
        var socket = new FakeWebSocket(new[] { FakeWebSocket.Text("""{"type":"ping"}""") });
        var (context, _) = BuildContext(socket);
        var middleware = CreateMiddleware(new[] { handler.Object }, new ConcurrentDictionary<string, WebSocketConnection>());

        // Act
        var act = async () => await middleware.InvokeAsync(context.Object);

        // Assert
        await act.Should().NotThrowAsync();
        handler.Verify(h => h.HandleSubscription(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
