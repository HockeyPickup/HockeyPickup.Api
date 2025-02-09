using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using System.Security.Claims;

namespace HockeyPickup.Api.Tests.ControllerTests;

public class BuySellControllerTests
{
    private readonly Mock<IBuySellService> _mockBuySellService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly BuySellController _controller;
    private const string TestUserId = "testUser123";
    private readonly DateTime _testDate = DateTime.UtcNow;

    public BuySellControllerTests()
    {
        _mockBuySellService = new Mock<IBuySellService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup HttpContext to return test user ID
        var mockHttpContext = new Mock<HttpContext>();
        var mockPrincipal = new Mock<ClaimsPrincipal>();
        mockPrincipal.Setup(x => x.FindFirst(ClaimTypes.NameIdentifier))
            .Returns(new Claim(ClaimTypes.NameIdentifier, TestUserId));
        mockHttpContext.Setup(x => x.User).Returns(mockPrincipal.Object);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        _controller = new BuySellController(_mockBuySellService.Object, _mockHttpContextAccessor.Object);
    }

    private BuySellResponse CreateTestBuySellResponse(int buySellId = 1)
    {
        return new BuySellResponse
        {
            BuySellId = buySellId,
            CreateDateTime = _testDate,
            UpdateDateTime = _testDate,
            TeamAssignment = TeamAssignment.Light,
            Price = 27.00m,
            PaymentSent = false,
            PaymentReceived = false,
            BuyerNoteFlagged = false,
            SellerNoteFlagged = false,
            SessionId = 1,
            SessionDate = _testDate,
        };
    }

    private BuySellStatusResponse CreateTestBuySellStatusResponse(bool isAllowed = true)
    {
        return new BuySellStatusResponse
        {
            IsAllowed = isAllowed,
            Reason = isAllowed ? "Test allowed" : "Test not allowed"
        };
    }

    [Fact]
    public async Task Buy_ValidRequest_ReturnsCreatedWithLocation()
    {
        // Arrange
        var buyRequest = new BuyRequest { SessionId = 1, Note = "Test buy" };
        var expected = CreateTestBuySellResponse();
        _mockBuySellService.Setup(s => s.ProcessBuyRequestAsync(TestUserId, buyRequest))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.Buy(buyRequest);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(BuySellController.GetBuySell));
        var response = createdResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Buy_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var buyRequest = new BuyRequest { SessionId = 1 };
        _mockBuySellService.Setup(s => s.ProcessBuyRequestAsync(TestUserId, buyRequest))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateFailure("Invalid request"));

        // Act
        var result = await _controller.Buy(buyRequest);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Sell_ValidRequest_ReturnsCreatedWithLocation()
    {
        // Arrange
        var sellRequest = new SellRequest { SessionId = 1, Note = "Test sell" };
        var expected = CreateTestBuySellResponse();
        _mockBuySellService.Setup(s => s.ProcessSellRequestAsync(TestUserId, sellRequest))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.Sell(sellRequest);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(BuySellController.GetBuySell));
        var response = createdResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ConfirmPaymentSent_ValidRequest_ReturnsOk()
    {
        // Arrange
        var buySellId = 1;
        var paymentMethod = PaymentMethodType.Venmo;
        var expected = CreateTestBuySellResponse(buySellId);
        _mockBuySellService.Setup(s => s.ConfirmPaymentSentAsync(TestUserId, buySellId, paymentMethod))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.ConfirmPaymentSent(buySellId, paymentMethod);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ConfirmPaymentReceived_ValidRequest_ReturnsOk()
    {
        // Arrange
        var buySellId = 1;
        var expected = CreateTestBuySellResponse(buySellId);
        _mockBuySellService.Setup(s => s.ConfirmPaymentReceivedAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.ConfirmPaymentReceived(buySellId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task UnconfirmPaymentSent_ValidRequest_ReturnsOk()
    {
        // Arrange
        var buySellId = 1;
        var expected = CreateTestBuySellResponse(buySellId);
        _mockBuySellService.Setup(s => s.UnconfirmPaymentSentAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.UnconfirmPaymentSent(buySellId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task UnconfirmPaymentReceived_ValidRequest_ReturnsOk()
    {
        // Arrange
        var buySellId = 1;
        var expected = CreateTestBuySellResponse(buySellId);
        _mockBuySellService.Setup(s => s.UnconfirmPaymentReceivedAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.UnconfirmPaymentReceived(buySellId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetBuySell_ValidId_ReturnsOk()
    {
        // Arrange
        var buySellId = 1;
        var expected = CreateTestBuySellResponse(buySellId);
        _mockBuySellService.Setup(s => s.GetBuySellAsync(buySellId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.GetBuySell(buySellId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetBuySell_InvalidId_ReturnsNotFound()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellService.Setup(s => s.GetBuySellAsync(buySellId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateFailure("Not found"));

        // Act
        var result = await _controller.GetBuySell(buySellId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSessionBuySells_ValidId_ReturnsOk()
    {
        // Arrange
        var sessionId = 1;
        var expected = new List<BuySellResponse> { CreateTestBuySellResponse() };
        _mockBuySellService.Setup(s => s.GetSessionBuySellsAsync(sessionId))
            .ReturnsAsync(ServiceResult<IEnumerable<BuySellResponse>>.CreateSuccess(expected));

        // Act
        var result = await _controller.GetSessionBuySells(sessionId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<IEnumerable<BuySellResponse>>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task CancelBuy_ValidRequest_ReturnsOk()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellService.Setup(s => s.CancelBuyAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<bool>.CreateSuccess(true));

        // Act
        var result = await _controller.CancelBuy(buySellId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<bool>>().Subject;
        response.Data.Should().BeTrue();
    }

    [Fact]
    public async Task CancelSell_ValidRequest_ReturnsOk()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellService.Setup(s => s.CancelSellAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<bool>.CreateSuccess(true));

        // Act
        var result = await _controller.CancelSell(buySellId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<bool>>().Subject;
        response.Data.Should().BeTrue();
    }

    [Fact]
    public async Task CanBuy_ValidRequest_ReturnsOk()
    {
        // Arrange
        var sessionId = 1;
        var expected = CreateTestBuySellStatusResponse();
        _mockBuySellService.Setup(s => s.CanBuyAsync(TestUserId, sessionId))
            .ReturnsAsync(ServiceResult<BuySellStatusResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.CanBuy(sessionId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<BuySellStatusResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task CanSell_ValidRequest_ReturnsOk()
    {
        // Arrange
        var sessionId = 1;
        var expected = CreateTestBuySellStatusResponse();
        _mockBuySellService.Setup(s => s.CanSellAsync(TestUserId, sessionId))
            .ReturnsAsync(ServiceResult<BuySellStatusResponse>.CreateSuccess(expected));

        // Act
        var result = await _controller.CanSell(sessionId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiDataResponse<BuySellStatusResponse>>().Subject;
        response.Data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Sell_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var sellRequest = new SellRequest { SessionId = 1, Note = "Test sell" };
        _mockBuySellService.Setup(s => s.ProcessSellRequestAsync(TestUserId, sellRequest))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.Sell(sellRequest);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmPaymentSent_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var buySellId = 1;
        var paymentMethod = PaymentMethodType.Venmo;
        _mockBuySellService.Setup(s => s.ConfirmPaymentSentAsync(TestUserId, buySellId, paymentMethod))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.ConfirmPaymentSent(buySellId, paymentMethod);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmPaymentReceived_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellService.Setup(s => s.ConfirmPaymentReceivedAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.ConfirmPaymentReceived(buySellId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UnconfirmPaymentSent_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellService.Setup(s => s.UnconfirmPaymentSentAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.UnconfirmPaymentSent(buySellId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UnconfirmPaymentReceived_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellService.Setup(s => s.UnconfirmPaymentReceivedAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<BuySellResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.UnconfirmPaymentReceived(buySellId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<BuySellResponse>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetSessionBuySells_ServiceReturnsFailure_ReturnsNotFound()
    {
        // Arrange
        var sessionId = 1;
        _mockBuySellService.Setup(s => s.GetSessionBuySellsAsync(sessionId))
            .ReturnsAsync(ServiceResult<IEnumerable<BuySellResponse>>.CreateFailure("Test error"));

        // Act
        var result = await _controller.GetSessionBuySells(sessionId);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeOfType<ApiDataResponse<IEnumerable<BuySellResponse>>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBuy_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellService.Setup(s => s.CancelBuyAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<bool>.CreateFailure("Test error"));

        // Act
        var result = await _controller.CancelBuy(buySellId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<bool>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CancelSell_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var buySellId = 1;
        _mockBuySellService.Setup(s => s.CancelSellAsync(TestUserId, buySellId))
            .ReturnsAsync(ServiceResult<bool>.CreateFailure("Test error"));

        // Act
        var result = await _controller.CancelSell(buySellId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<bool>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CanBuy_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = 1;
        _mockBuySellService.Setup(s => s.CanBuyAsync(TestUserId, sessionId))
            .ReturnsAsync(ServiceResult<BuySellStatusResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.CanBuy(sessionId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<BuySellStatusResponse>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CanSell_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = 1;
        _mockBuySellService.Setup(s => s.CanSellAsync(TestUserId, sessionId))
            .ReturnsAsync(ServiceResult<BuySellStatusResponse>.CreateFailure("Test error"));

        // Act
        var result = await _controller.CanSell(sessionId);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiDataResponse<BuySellStatusResponse>>().Subject;
        response.Success.Should().BeFalse();
    }
}
