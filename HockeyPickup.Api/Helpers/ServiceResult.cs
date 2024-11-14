namespace HockeyPickup.Api.Helpers;

// Common/ServiceResult.cs
public class ServiceResult
{
    public bool IsSuccess { get; protected set; }
    public string Message { get; protected set; }

    protected ServiceResult(bool isSuccess, string message = "")
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static ServiceResult CreateSuccess(string message = "")
        => new ServiceResult(true, message);

    public static ServiceResult CreateFailure(string message)
        => new ServiceResult(false, message);
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private set; }

    private ServiceResult(bool isSuccess, string message = "", T? data = default)
        : base(isSuccess, message)
    {
        Data = data;
    }

    public static ServiceResult<T> CreateSuccess(T data, string message = "")
        => new ServiceResult<T>(true, message, data);

    public static new ServiceResult<T> CreateFailure(string message)
        => new ServiceResult<T>(false, message);
}
