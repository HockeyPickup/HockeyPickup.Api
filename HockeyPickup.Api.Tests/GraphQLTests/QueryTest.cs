using Moq;
using HockeyPickup.Api.Models.Responses;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Data.GraphQL;
using Microsoft.AspNetCore.Http;
using HotChocolate.Types;
using System.Reflection;
using System.Security.Claims;

namespace HockeyPickup.Api.Tests.Data.GraphQL;

public class GraphQLTests
{
    [Fact]
    public void UserBasicType_ShouldConfigureCorrectly()
    {
        // Arrange
        var descriptorMock = new Mock<IObjectTypeDescriptor<UserBasicResponse>>();

        // Act
        var userBasicType = new UserBasicType();
        // Use reflection to invoke the protected Configure method, specifying parameter type explicitly
        var configureMethod = typeof(UserBasicType).GetMethod("Configure", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(IObjectTypeDescriptor<UserBasicResponse>) }, null);
        configureMethod.Invoke(userBasicType, new object[] { descriptorMock.Object });

        // Assert
        descriptorMock.Verify(d => d.Name("User"), Times.Once);
    }

    [Fact]
    public void UserDetailedType_ShouldConfigureCorrectly()
    {
        // Arrange
        var descriptorMock = new Mock<IObjectTypeDescriptor<UserDetailedResponse>>();

        // Act
        var userDetailedType = new UserDetailedType();
        // Use reflection to invoke the protected Configure method
        var configureMethod = typeof(UserDetailedType).GetMethod("Configure", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(IObjectTypeDescriptor<UserDetailedResponse>) }, null);
        configureMethod.Invoke(userDetailedType, new object[] { descriptorMock.Object });

        // Assert
        descriptorMock.Verify(d => d.Name("UserDetailed"), Times.Once);
    }

    [Fact]
    public void UserResponseType_ShouldConfigureCorrectly()
    {
        // Arrange
        var descriptorMock = new Mock<IUnionTypeDescriptor>();

        // Set up the mock to respond to the method calls you expect
        descriptorMock.Setup(d => d.Name(It.IsAny<string>())).Returns(descriptorMock.Object);
        descriptorMock.Setup(d => d.Description(It.IsAny<string>())).Returns(descriptorMock.Object);
        descriptorMock.Setup(d => d.Type<UserBasicType>()).Returns(descriptorMock.Object);
        descriptorMock.Setup(d => d.Type<UserDetailedType>()).Returns(descriptorMock.Object);

        // Act
        var userResponseType = new UserResponseType();
        var configureMethod = typeof(UserResponseType).GetMethod("Configure", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(IUnionTypeDescriptor) }, null);
        configureMethod.Invoke(userResponseType, new object[] { descriptorMock.Object });

        // Assert
        descriptorMock.Verify(d => d.Name("UserResponse"), Times.Once);
        descriptorMock.Verify(d => d.Description("Represents either a basic or detailed user response"), Times.Once);
        descriptorMock.Verify(d => d.Type<UserBasicType>(), Times.Once);
        descriptorMock.Verify(d => d.Type<UserDetailedType>(), Times.Once);
    }

    [Fact]
    public async Task Query_Users_ShouldReturnDetailedUsers_WhenAdmin()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockUserRepository = new Mock<IUserRepository>();

        mockHttpContextAccessor.Setup(h => h.HttpContext.User.IsInRole("Admin")).Returns(true);
        mockUserRepository.Setup(r => r.GetDetailedUsersAsync())
                          .ReturnsAsync(new List<UserDetailedResponse>
                          {
                              new UserDetailedResponse
                              {
                                  Id = "user123",
                                  UserName = "adminUser",
                                  Active = true,
                                  IsPreferred = true,
                                  IsPreferredPlus = true,
                                  Rating = 4.5m
                              }
                          });

        var query = new Query(mockHttpContextAccessor.Object);

        // Act
        var result = await query.Users(mockUserRepository.Object);

        // Assert
        var resultList = result.Cast<UserDetailedResponse>().ToList();
        Assert.Single(resultList);
        Assert.Equal("user123", resultList[0].Id);
        Assert.Equal("adminUser", resultList[0].UserName);
        mockUserRepository.Verify(r => r.GetDetailedUsersAsync(), Times.Once);
    }

    [Fact]
    public async Task Query_Users_ShouldReturnBasicUsers_WhenNotAdmin()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockUserRepository = new Mock<IUserRepository>();

        mockHttpContextAccessor.Setup(h => h.HttpContext.User.IsInRole("Admin")).Returns(false);
        mockUserRepository.Setup(r => r.GetBasicUsersAsync())
                          .ReturnsAsync(new List<UserBasicResponse>
                          {
                              new UserBasicResponse
                              {
                                  Id = "user123",
                                  UserName = "basicUser",
                                  Active = true,
                                  IsPreferred = false,
                                  IsPreferredPlus = false
                              }
                          });

        var query = new Query(mockHttpContextAccessor.Object);

        // Act
        var result = await query.Users(mockUserRepository.Object);

        // Assert
        var resultList = result.Cast<UserBasicResponse>().ToList();
        Assert.Single(resultList);
        Assert.Equal("user123", resultList[0].Id);
        Assert.Equal("basicUser", resultList[0].UserName);
        mockUserRepository.Verify(r => r.GetBasicUsersAsync(), Times.Once);
    }

    [Fact]
    public async Task Query_Users_ShouldReturnEmpty_WhenNoUsers()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockUserRepository = new Mock<IUserRepository>();

        mockHttpContextAccessor.Setup(h => h.HttpContext.User.IsInRole("Admin")).Returns(true);
        mockUserRepository.Setup(r => r.GetDetailedUsersAsync())
                          .ReturnsAsync(new List<UserDetailedResponse>());

        var query = new Query(mockHttpContextAccessor.Object);

        // Act
        var result = await query.Users(mockUserRepository.Object);

        // Assert
        var resultList = result.Cast<UserDetailedResponse>().ToList();
        Assert.Empty(resultList);
        mockUserRepository.Verify(r => r.GetDetailedUsersAsync(), Times.Once);
    }

    [Fact]
    public async Task Users_ShouldReturnBasicUsers_WhenNotAdmin()
    {
        // Arrange: Set up a mock IHttpContextAccessor with a non-admin user
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var httpContextMock = new Mock<HttpContext>();
        var userMock = new Mock<ClaimsPrincipal>();

        // Set up the scenario where HttpContext is not null, but User is not an Admin
        userMock.Setup(u => u.IsInRole("Admin")).Returns(false);
        httpContextMock.Setup(c => c.User).Returns(userMock.Object);
        httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContextMock.Object);

        var query = new Query(httpContextAccessorMock.Object);
        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(repo => repo.GetBasicUsersAsync()).ReturnsAsync(new List<UserBasicResponse>());

        // Act: Call the method
        var result = await query.Users(userRepositoryMock.Object);

        // Assert: Check that basic users were returned (since the user is not an admin)
        Assert.IsType<List<UserBasicResponse>>(result);
    }

    [Fact]
    public async Task Users_ShouldReturnDetailedUsers_WhenAdmin()
    {
        // Arrange: Set up a mock IHttpContextAccessor with an admin user
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var httpContextMock = new Mock<HttpContext>();
        var userMock = new Mock<ClaimsPrincipal>();

        // Set up the scenario where HttpContext is not null, and User is an Admin
        userMock.Setup(u => u.IsInRole("Admin")).Returns(true);
        httpContextMock.Setup(c => c.User).Returns(userMock.Object);
        httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContextMock.Object);

        var query = new Query(httpContextAccessorMock.Object);
        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(repo => repo.GetDetailedUsersAsync()).ReturnsAsync(new List<UserDetailedResponse>());

        // Act: Call the method
        var result = await query.Users(userRepositoryMock.Object);

        // Assert: Check that detailed users were returned (since the user is an admin)
        Assert.IsType<List<UserDetailedResponse>>(result);
    }

    [Fact]
    public async Task Users_ShouldReturnBasicUsers_WhenHttpContextIsNull()
    {
        // Arrange: Set up a mock IHttpContextAccessor with HttpContext being null
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext) null);

        var query = new Query(httpContextAccessorMock.Object);
        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(repo => repo.GetBasicUsersAsync()).ReturnsAsync(new List<UserBasicResponse>());

        // Act: Call the method
        var result = await query.Users(userRepositoryMock.Object);

        // Assert: Check that basic users were returned (since HttpContext is null)
        Assert.IsType<List<UserBasicResponse>>(result);
    }

    [Fact]
    public async Task Users_ShouldReturnBasicUsers_WhenUserIsNull()
    {
        // Arrange: Set up a mock IHttpContextAccessor with a null User
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var httpContextMock = new Mock<HttpContext>();

        // Set up the scenario where HttpContext is not null, but User is null
        httpContextMock.Setup(c => c.User).Returns((ClaimsPrincipal) null);
        httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContextMock.Object);

        var query = new Query(httpContextAccessorMock.Object);
        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(repo => repo.GetBasicUsersAsync()).ReturnsAsync(new List<UserBasicResponse>());

        // Act: Call the method
        var result = await query.Users(userRepositoryMock.Object);

        // Assert: Check that basic users were returned (since User is null)
        Assert.IsType<List<UserBasicResponse>>(result);
    }
}
