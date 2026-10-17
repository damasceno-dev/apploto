namespace server.Domain.Models.Projections;

/// <summary>
/// A client's branch-wide fiado position as of a date (§6.4): the signed <c>Out − In</c> balance across every Tab
/// (negative = client credit) and the earliest sale <c>Date</c> among the sales still holding a positive unpaid
/// remainder after FIFO netting (null when none remains).
/// </summary>
public record FiadoClientPosition(decimal OutstandingTotal, DateTime? OldestUnpaidDate);
