namespace server.Domain.Models;

/// <summary>The unchanged §6.12 arithmetic, shared by operational calculation and read-only import projections.</summary>
public static class CashVarianceFormula
{
    public static decimal Calculate(decimal totalClosing, decimal totalOpening, decimal totalIn, decimal totalOut) =>
        totalClosing - totalOpening - (totalIn - totalOut);
}
