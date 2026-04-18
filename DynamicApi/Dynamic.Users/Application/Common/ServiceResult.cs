namespace Dynamic.Users.Application.Common;

public class ServiceResult
{
    public bool Succeeded { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ServiceResult Success()
        => new() { Succeeded = true };

    public static ServiceResult Failure(string errorCode, string errorMessage)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Success(T data)
        => new()
        {
            Succeeded = true,
            Data = data
        };

    public static new ServiceResult<T> Failure(string errorCode, string errorMessage)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
}
