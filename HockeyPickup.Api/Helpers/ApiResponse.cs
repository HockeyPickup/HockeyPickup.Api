using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace HockeyPickup.Api.Helpers;

public interface IApiResponse
{
    bool Success { get; set; }
    string? Message { get; set; }
    List<ErrorDetail> Errors { get; set; }
}

[Description("Generic API response wrapper")]
public class ApiResponse : IApiResponse
{
    [Required]
    [Description("Indicates if the operation was successful")]
    [JsonPropertyName("Success")]
    [JsonProperty(nameof(Success), Required = Required.Always)]
    public bool Success { get; set; }

    [Description("Optional message providing additional context about the operation")]
    [DataType(DataType.Text)]
    [MaxLength(500)]
    [JsonPropertyName("Message")]
    [JsonProperty(nameof(Message), Required = Required.Default)]
    public string? Message { get; set; }

    [Required]
    [Description("List of error details if operation was not successful")]
    [JsonPropertyName("Errors")]
    [JsonProperty(nameof(Errors), Required = Required.Always)]
    public List<ErrorDetail> Errors { get; set; } = [];

    public static ApiResponse FromServiceResult(ServiceResult result)
    {
        return new ApiResponse
        {
            Success = result.IsSuccess,
            Message = result.Message,
            Errors = result.IsSuccess
                ? []
                : [new ErrorDetail { Code = "SERVICE_ERROR", Message = result.Message }]
        };
    }
}

[Description("Generic API response wrapper with typed data payload")]
public class ApiDataResponse<T> : ApiResponse
{
    [Description("Response data payload of type T")]
    [JsonPropertyName("Data")]
    [JsonProperty(nameof(Data), Required = Required.Default)]
    public T? Data { get; set; }

    public static ApiDataResponse<T> FromServiceResult(ServiceResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ApiDataResponse<T>
        {
            Success = result.IsSuccess,
            Message = result.Message,
            Data = result.Data,
            Errors = result.IsSuccess
                ? []
                : [new ErrorDetail { Code = "SERVICE_ERROR", Message = result.Message }]
        };
    }
}

public static class ApiResponseExtensions
{
    public static ApiDataResponse<T> ToApiDataResponse<T>(this T data, ServiceResult result)
        => new()
        {
            Success = true,
            Message = result.Message,
            Data = data,
            Errors = []
        };

    [ExcludeFromCodeCoverage]
    public static ApiDataResponse<T> ToApiDataResponse<T>(this T data, string message = "Success")
        => new()
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = []
        };

    // New method to create error response from any ServiceResult
    public static ApiDataResponse<T> ToErrorResponse<T>(this ServiceResult result, string code = "SERVICE_ERROR")
        => new()
        {
            Success = false,
            Message = result.Message,
            Data = default,
            Errors = [new ErrorDetail { Code = code, Message = result.Message }]
        };
}

[Description("Detailed error information")]
public class ErrorDetail
{
    [Description("Error code identifying the type of error")]
    [DataType(DataType.Text)]
    [MaxLength(50)]
    [JsonPropertyName("Code")]
    [JsonProperty(nameof(Code), Required = Required.Default)]
    public string? Code { get; set; }

    [Description("Human-readable error message")]
    [DataType(DataType.Text)]
    [MaxLength(500)]
    [JsonPropertyName("Message")]
    [JsonProperty(nameof(Message), Required = Required.Default)]
    public string? Message { get; set; }

    [Description("Name of the field that caused the error, if applicable")]
    [DataType(DataType.Text)]
    [MaxLength(100)]
    [JsonPropertyName("Field")]
    [JsonProperty(nameof(Field), Required = Required.Default)]
    public string? Field { get; set; }
}
