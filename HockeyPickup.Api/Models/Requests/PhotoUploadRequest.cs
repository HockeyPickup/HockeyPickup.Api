using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HockeyPickup.Api.Models.Requests;

public class AdminPhotoUploadRequest
{
    [Required]
    [Description("User identifier for whom to upload the photo")]
    [MaxLength(128)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    public required string UserId { get; set; }

    [Required]
    [Description("Profile photo file (JPG or PNG only)")]
    [DataType(DataType.Upload)]
    [JsonPropertyName("File")]
    [JsonProperty(nameof(File), Required = Required.Always)]
    public required IFormFile File { get; set; }
}

public class AdminPhotoDeleteRequest
{
    [Required]
    [Description("User identifier whose photo should be deleted")]
    [MaxLength(128)]
    [JsonPropertyName("UserId")]
    [JsonProperty(nameof(UserId), Required = Required.Always)]
    public required string UserId { get; set; }
}

public class UploadPhotoRequest
{
    [Required]
    [Description("Profile photo file (JPG or PNG only)")]
    [DataType(DataType.Upload)]
    [JsonPropertyName("File")]
    [JsonProperty(nameof(File), Required = Required.Always)]
    public required IFormFile File { get; set; }
}
