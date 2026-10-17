namespace server.Domain.Entities.Enums;

/// <summary>
/// What a fiado repayment method moves (M7.7 decision 1.3 amendment). The method is the repayment transaction type;
/// this marker, configured on the type, is the only thing that decides whether a Terminal cash leg is generated.
/// </summary>
public enum FiadoPaymentKind
{
    /// <summary>Cash received into the paired terminal's counted drawer: Tab <c>In</c> plus a generated Terminal <c>In</c>.</summary>
    CountedCash = 1,

    /// <summary>Any method that does not enter the counted drawer (PIX, card, transfer…): Tab <c>In</c> only.</summary>
    NonCash = 2
}
