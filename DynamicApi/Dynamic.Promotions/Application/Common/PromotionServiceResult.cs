namespace Dynamic.Promotions.Application.Common;

public class PromotionServiceResult<T>
{
    public bool Succeeded { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public T? Data { get; init; }

    public static PromotionServiceResult<T> Success(T data)
        => new() { Succeeded = true, Data = data };

    public static PromotionServiceResult<T> Failure(string errorCode, string errorMessage)
        => new() { ErrorCode = errorCode, ErrorMessage = errorMessage };
}
