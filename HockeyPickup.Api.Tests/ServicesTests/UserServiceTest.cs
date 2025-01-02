using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Azure.Storage.Blobs;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class UserServiceTest
{
    private readonly Mock<UserManager<AspNetUser>> _mockUserManager;
    private readonly Mock<SignInManager<AspNetUser>> _mockSignInManager;
    private readonly Mock<IServiceBus> _mockServiceBus;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;

    private readonly UserService _service;

    public UserServiceTest()
    {
        // UserManager requires these for constructor
        var userStore = new Mock<IUserStore<AspNetUser>>();
        var mockOptions = new Mock<IOptions<IdentityOptions>>();
        var mockPasswordHasher = new Mock<IPasswordHasher<AspNetUser>>();
        var mockUserValidators = new List<IUserValidator<AspNetUser>>();
        var mockPasswordValidators = new List<IPasswordValidator<AspNetUser>>();
        var mockKeyNormalizer = new Mock<ILookupNormalizer>();
        var mockErrors = new Mock<IdentityErrorDescriber>();
        var mockServices = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<UserManager<AspNetUser>>>();

        _mockUserManager = new Mock<UserManager<AspNetUser>>(
            userStore.Object,
            mockOptions.Object,
            mockPasswordHasher.Object,
            mockUserValidators,
            mockPasswordValidators,
            mockKeyNormalizer.Object,
            mockErrors.Object,
            mockServices.Object,
            mockLogger.Object);

        // SignInManager requires these for constructor
        var mockContextAccessor = new Mock<IHttpContextAccessor>();
        var mockClaimsFactory = new Mock<IUserClaimsPrincipalFactory<AspNetUser>>();
        var mockSignInLogger = new Mock<ILogger<SignInManager<AspNetUser>>>();
        var mockAuthenticationSchemeProvider = new Mock<IAuthenticationSchemeProvider>();
        var mockUserConfirmation = new Mock<IUserConfirmation<AspNetUser>>();

        _mockSignInManager = new Mock<SignInManager<AspNetUser>>(
            _mockUserManager.Object,
            mockContextAccessor.Object,
            mockClaimsFactory.Object,
            mockOptions.Object,
            mockSignInLogger.Object,
            mockAuthenticationSchemeProvider.Object,
            mockUserConfirmation.Object);

        _mockServiceBus = new Mock<IServiceBus>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<UserService>>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();

        _service = new UserService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockServiceBus.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockBlobServiceClient.Object
        );
    }
}
