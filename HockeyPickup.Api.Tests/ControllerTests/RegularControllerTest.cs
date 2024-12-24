using HockeyPickup.Api.Controllers;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Requests;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace HockeyPickup.Api.Tests.ControllerTests;

public class RegularControllerTests
{
    private readonly Mock<IRegularService> _regularService;
    private readonly Mock<ILogger<AuthController>> _logger;
    private readonly RegularController _controller;

    public RegularControllerTests()
    {
        _regularService = new Mock<IRegularService>();
        _logger = new Mock<ILogger<AuthController>>();
        _controller = new RegularController(_regularService.Object);
    }

    private static RegularSetDetailedResponse CreateTestRegularSet()
    {
        return new RegularSetDetailedResponse
        {
            RegularSetId = 1,
            Description = "Test Regular Set",
            DayOfWeek = 1,
            Archived = false,
            CreateDateTime = DateTime.UtcNow,
            Regulars = new List<RegularDetailedResponse>()
        };
    }

    [Fact]
    public async Task DuplicateRegularSet_Success_ReturnsCreatedResponse()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "New Regular Set"
        };

        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateSuccess(
            CreateTestRegularSet(), "Success");

        _regularService.Setup(x => x.DuplicateRegularSet(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DuplicateRegularSet(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(createdResult.Value);
        Assert.True(response.Success);
        Assert.Equal(1, response.Data.RegularSetId);
    }

    [Fact]
    public async Task DuplicateRegularSet_Failure_ReturnsBadRequest()
    {
        // Arrange
        var request = new DuplicateRegularSetRequest
        {
            RegularSetId = 1,
            Description = "New Regular Set"
        };

        var serviceResponse = ServiceResult<RegularSetDetailedResponse>.CreateFailure(
            "Test error");

        _regularService.Setup(x => x.DuplicateRegularSet(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DuplicateRegularSet(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiDataResponse<RegularSetDetailedResponse>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Test error", response.Message);
    }
}
