namespace Dynamic.Reports.Application.Common;

public sealed class ReportResult<T>
{
    public bool Succeeded { get; private init; }
    public T? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ReportResult<T> Success(T data) => new() { Succeeded = true, Data = data };
    public static ReportResult<T> Failure(string code, string message) => new() { ErrorCode = code, ErrorMessage = message };
}
