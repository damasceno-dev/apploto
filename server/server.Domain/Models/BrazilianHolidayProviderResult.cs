namespace server.Domain.Models;

public sealed record BrazilianHolidayProviderResult<T>(bool Success,T? Data,string? FailureReason)
{
    public static BrazilianHolidayProviderResult<T> SuccessResult(T data) => new(true, data, null);

    public static BrazilianHolidayProviderResult<T> Failure(string reason) => new(false, default, reason);
}
