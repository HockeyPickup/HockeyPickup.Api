using Microsoft.AspNetCore.Identity;
using Moq;
using FluentAssertions;
using HockeyPickup.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure;
using Microsoft.Extensions.Logging;

namespace HockeyPickup.Api.Tests.ServicesTests;

public partial class UserServiceTest
{
    private IFormFile CreateMockFormFile(string fileName, string contentType, long length)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return file.Object;
    }

    private Mock<BlobContainerClient> SetupMockBlobContainer(string blobUri = "https://storage.test/container/test-blob")
    {
        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        var mockResponse = new Mock<Response>();

        // Setup blob client
        mockBlobClient.Setup(b => b.Uri).Returns(new Uri(blobUri));

        // Setup blob upload
        var mockBlobContentInfo = new Mock<BlobContentInfo>();
        var mockBlobContentResponse = new Mock<Response<BlobContentInfo>>();
        mockBlobContentResponse.Setup(r => r.Value).Returns(mockBlobContentInfo.Object);
        mockBlobContentResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockBlobClient.Setup(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(mockBlobContentResponse.Object));

        // Setup blob delete
        var mockDeleteResponse = new Mock<Response<bool>>();
        mockDeleteResponse.Setup(r => r.Value).Returns(true);
        mockDeleteResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockBlobClient.Setup(b => b.DeleteIfExistsAsync(
            DeleteSnapshotsOption.None,
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(mockDeleteResponse.Object));

        // Setup container
        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        var mockBlobContainerInfo = new Mock<BlobContainerInfo>();
        var mockContainerResponse = new Mock<Response<BlobContainerInfo>>();
        mockContainerResponse.Setup(r => r.Value).Returns(mockBlobContainerInfo.Object);
        mockContainerResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockContainerClient.Setup(c => c.CreateIfNotExistsAsync(
            PublicAccessType.Blob,
            It.IsAny<IDictionary<string, string>>(),
            default,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockContainerResponse.Object);

        var mockAccessPolicyResponse = new Mock<Response<BlobContainerInfo>>();
        mockAccessPolicyResponse.Setup(r => r.Value).Returns(mockBlobContainerInfo.Object);
        mockAccessPolicyResponse.Setup(r => r.GetRawResponse()).Returns(mockResponse.Object);

        mockContainerClient.Setup(c => c.SetAccessPolicyAsync(
            PublicAccessType.Blob,
            It.IsAny<IEnumerable<BlobSignedIdentifier>>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAccessPolicyResponse.Object);

        return mockContainerClient;
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_ValidFile_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(blobUri);
        user.PhotoUrl.Should().Be(blobUri);
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "nonexistent-user";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Theory]
    [InlineData("photo.txt")]
    [InlineData("photo.gif")]
    [InlineData("photo.bmp")]
    public async Task UploadProfilePhotoAsync_InvalidFileType_ReturnsFailure(string fileName)
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile(fileName, "image/gif", 1024 * 1024);
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid file type. Only JPG and PNG files are allowed");
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task DeleteProfilePhotoAsync_Success_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = SetupMockBlobContainer(photoUrl);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_NoPhotoExists_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new AspNetUser { Id = userId, PhotoUrl = null };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("No photo to delete");
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_BlobStorageError_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();

        mockBlobClient.Setup(b => b.DeleteIfExistsAsync(
            DeleteSnapshotsOption.None,
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Blob storage error"));

        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while deleting the photo");
    }

    [Fact]
    public async Task AdminUploadProfilePhotoAsync_ValidFile_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.AdminUploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(blobUri);
    }

    [Fact]
    public async Task AdminDeleteProfilePhotoAsync_Success_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = SetupMockBlobContainer(photoUrl);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.AdminDeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PhotoUrl.Should().BeNull();
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task UploadProfilePhotoAsync_NullFile_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("No file uploaded");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_BlobUploadFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        var mockResponse = new Mock<Response>();

        mockBlobClient.Setup(b => b.Uri).Returns(new Uri("https://test.com/blob"));
        mockBlobClient.Setup(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Upload failed"));

        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while processing the photo");
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_BlobDeleteFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();

        mockBlobClient.Setup(b => b.DeleteIfExistsAsync(
            It.IsAny<DeleteSnapshotsOption>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Failed to delete blob"));

        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while deleting the photo");
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_UserUpdateFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = SetupMockBlobContainer();
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new[] { new IdentityError { Description = "Failed to update user" } }));

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update user");
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task UploadProfilePhotoAsync_FileEmpty_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 0);
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("No file uploaded");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_UpdateSecurityStampFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new[] { new IdentityError { Description = "Failed to update user" } }));
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new[] { new IdentityError { Description = "Failed to update security stamp" } }));

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update user");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_ContainerCreateFailure_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };

        var mockContainerClient = new Mock<BlobContainerClient>();
        mockContainerClient.Setup(c => c.CreateIfNotExistsAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Failed to create container"));

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while processing the photo");
    }

    [Theory]
    [InlineData((5 * 1024 * 1024) + 1)]
    public async Task UploadProfilePhotoAsync_FileTooLarge_ReturnsFailure(long fileSize)
    {
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", fileSize);
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var result = await _service.UploadProfilePhotoAsync(userId, file);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("File size exceeds 5MB limit");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_UserUpdateFailure_ReturnsFailure()
    {
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024);
        var user = new AspNetUser { Id = userId };

        var mockContainerClient = SetupMockBlobContainer();
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new[] { new IdentityError { Description = "Update failed" } }));

        var result = await _service.UploadProfilePhotoAsync(userId, file);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Update failed");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_SetAccessPolicyFailure_ReturnsFailure()
    {
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024);
        var user = new AspNetUser { Id = userId };

        var mockContainerClient = new Mock<BlobContainerClient>();
        mockContainerClient.Setup(c => c.SetAccessPolicyAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IEnumerable<BlobSignedIdentifier>>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Access policy error"));

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var result = await _service.UploadProfilePhotoAsync(userId, file);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while processing the photo");
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_UserNotFound_ReturnsFailure()
    {
        var userId = "test-user-id";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null);

        var result = await _service.DeleteProfilePhotoAsync(userId);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task DeleteProfilePhotoAsync_BlobDeleteFails_ReturnsFailure()
    {
        var userId = "test-user-id";
        var photoUrl = "https://storage.test/container/photo.jpg";
        var user = new AspNetUser { Id = userId, PhotoUrl = photoUrl };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();

        mockBlobClient.Setup(b => b.DeleteIfExistsAsync(
            It.IsAny<DeleteSnapshotsOption>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Delete failed"));

        mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var result = await _service.DeleteProfilePhotoAsync(userId);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Delete failed");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_UserUpdateFailsAfterBlobUpload_ReturnsFailure()
    {
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024);
        var user = new AspNetUser { Id = userId };

        var mockContainerClient = SetupMockBlobContainer();
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new[] { new IdentityError { Description = "Update failed" } }));

        var result = await _service.UploadProfilePhotoAsync(userId, file);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Update failed");
    }
}

public partial class UserServiceTest
{
    [Fact]
    public async Task UploadProfilePhotoAsync_NullExtension_ReturnsFailure()
    {
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo", "image/jpeg", 1024);
        var user = new AspNetUser { Id = userId };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var result = await _service.UploadProfilePhotoAsync(userId, file);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Invalid file type. Only JPG and PNG files are allowed");
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    public async Task UploadProfilePhotoAsync_EmptyUserId_ReturnsFailure(string? userId)
    {
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024);

        var result = await _service.UploadProfilePhotoAsync(userId, file);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_ContainerCreationAndSetupFails_ReturnsFailure()
    {
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024);
        var user = new AspNetUser { Id = userId };

        var mockContainerClient = new Mock<BlobContainerClient>();
        mockContainerClient.Setup(c => c.CreateIfNotExistsAsync(
            It.IsAny<PublicAccessType>(),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Container creation failed"));

        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var result = await _service.UploadProfilePhotoAsync(userId, file);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while processing the photo");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while uploading the photo. Error: Test exception");
    }

    [Fact]
    public async Task DeleteProfilePhotoAsync_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.DeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while deleting the photo. Error: Test exception");
    }

    [Fact]
    public async Task AdminUploadProfilePhotoAsync_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.AdminUploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while uploading the photo. Error: Test exception");
    }

    [Fact]
    public async Task AdminUploadProfilePhotoAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "nonexistent-user";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.AdminUploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task AdminDeleteProfilePhotoAsync_ExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.AdminDeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("An error occurred while deleting the photo. Error: Test exception");
    }

    [Fact]
    public async Task AdminDeleteProfilePhotoAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "nonexistent-user";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AspNetUser) null!);

        // Act
        var result = await _service.AdminDeleteProfilePhotoAsync(userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task ProcessPhotoUploadAsync_UserHasExistingPhoto_DeletesOldPhoto()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id", PhotoUrl = "https://storage.test/container/old-photo.jpg" };
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.UploadProfilePhotoAsync(user.Id, file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(blobUri);
        user.PhotoUrl.Should().Be(blobUri);
        _mockBlobServiceClient.Verify(b => b.GetBlobContainerClient(It.IsAny<string>()), Times.Once);
        mockContainerClient.Verify(c => c.GetBlobClient(It.Is<string>(s => s.Contains("old-photo.jpg"))), Times.Once);
        mockContainerClient.Verify(c => c.GetBlobClient(It.Is<string>(s => s.Contains("test-user-id"))), Times.Once);
    }

    [Fact]
    public async Task ProcessPhotoUploadAsync_UserHasNoExistingPhoto_UploadsNewPhoto()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id", PhotoUrl = null };
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.UploadProfilePhotoAsync(user.Id, file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(blobUri);
        user.PhotoUrl.Should().Be(blobUri);
        _mockBlobServiceClient.Verify(b => b.GetBlobContainerClient(It.IsAny<string>()), Times.Once);
        mockContainerClient.Verify(c => c.GetBlobClient(It.Is<string>(s => s.Contains("test-user-id"))), Times.Once);
    }

    [Fact]
    public async Task ProcessPhotoUploadAsync_UpdateUserFailsWithError_ReturnsFailure()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id", PhotoUrl = null };
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);

        var mockContainerClient = SetupMockBlobContainer();
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        // Act
        var result = await _service.UploadProfilePhotoAsync(user.Id, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Update failed");
    }

    [Fact]
    public async Task ProcessPhotoUploadAsync_UpdateUserFailsWithoutError_ReturnsFailure()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id", PhotoUrl = null };
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);

        var mockContainerClient = SetupMockBlobContainer();
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed());

        // Act
        var result = await _service.UploadProfilePhotoAsync(user.Id, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update user photo URL");
    }

    [Fact]
    public async Task ProcessPhotoDeleteAsync_UpdateUserFailsWithError_ReturnsFailure()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id", PhotoUrl = "https://storage.test/container/photo.jpg" };

        var mockContainerClient = SetupMockBlobContainer();
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        // Act
        var result = await _service.DeleteProfilePhotoAsync(user.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Update failed");
    }

    [Fact]
    public async Task ProcessPhotoDeleteAsync_UpdateUserFailsWithoutError_ReturnsFailure()
    {
        // Arrange
        var user = new AspNetUser { Id = "test-user-id", PhotoUrl = "https://storage.test/container/photo.jpg" };

        var mockContainerClient = SetupMockBlobContainer();
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed());

        // Act
        var result = await _service.DeleteProfilePhotoAsync(user.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Failed to update user");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_SecurityStampSucceedsButUpdateFails_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new[] { new IdentityError { Description = "Update failed" } }));

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Update failed");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_SecurityStampFails_ReturnsFailure()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ThrowsAsync(new InvalidOperationException("Security stamp update failed"));

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Security stamp update failed");
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_CompleteSuccess_ReturnsSuccess()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(blobUri);
        result.Data.UpdateDateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UploadProfilePhotoAsync_CompletesFullPath_ReturnsSuccessWithPhotoResponse()
    {
        // Arrange
        var userId = "test-user-id";
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 1024 * 1024);
        var user = new AspNetUser { Id = userId };
        var blobUri = "https://storage.test/container/test-blob";
        var timeBeforeTest = DateTime.UtcNow;

        var mockContainerClient = SetupMockBlobContainer(blobUri);
        _mockBlobServiceClient.Setup(b => b.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _service.UploadProfilePhotoAsync(userId, file);
        var timeAfterTest = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PhotoUrl.Should().Be(blobUri);
        result.Data.UpdateDateTime.Should().BeOnOrAfter(timeBeforeTest);
        result.Data.UpdateDateTime.Should().BeOnOrBefore(timeAfterTest);

        // Verify all mocks were called in order
        _mockUserManager.Verify(x => x.FindByIdAsync(userId), Times.Once());
        _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once());
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once());
    }
}
