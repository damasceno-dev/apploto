using FluentValidation;
using server.Exceptions;

namespace server.Application.Services;

public static class SharedValidators
{
    private const int MinimumPasswordLength = 8;
    public const int RefreshTokenExpirationTimeInDays = 7;

    /// <summary>
    /// Exclusive upper bound for a <c>numeric(14,2)</c> column.
    /// 12 integer digits + 2 decimal places → any value &gt;= 1 000 000 000 000 overflows.
    /// Shared by every feature that persists a monetary or quantity decimal in that column type.
    /// </summary>
    public const decimal Numeric14X2UpperBound = 1_000_000_000_000m;

    public static void ValidatePassword<T>(this IRuleBuilder<T, string> passwordRule)
    {
        passwordRule.NotEmpty().WithMessage(ResourcesErrorMessages.PASSWORD_EMPTY);
        passwordRule.Must(p => string.IsNullOrWhiteSpace(p) || p.Length >= MinimumPasswordLength).WithMessage(string.Format(ResourcesErrorMessages.PASSWORD_LENGTH, MinimumPasswordLength));
    }

    extension<T>(IRuleBuilder<T, decimal> rule)
    {
        /// <summary>
        /// Rejects values with more than 2 decimal places or with 13+ integer digits
        /// (i.e. &gt;= <see cref="Numeric14X2UpperBound"/>). The caller supplies the
        /// feature-specific error message key so each domain retains its own wording.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public void ValuePrecisionWithin14x2(string errorMessage)
        {
            rule
                .Must(v => decimal.Round(v, 2) == v && v < Numeric14X2UpperBound)
                .WithMessage(errorMessage);
        }
    }
}