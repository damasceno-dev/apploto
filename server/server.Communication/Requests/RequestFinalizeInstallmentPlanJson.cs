namespace server.Communication.Requests;

/// <summary>
/// The caller's expected Draft installments of the plan, each with the version it loaded. The server
/// finalizes only when this set equals the plan's current Draft installments and every version matches.
/// </summary>
public class RequestFinalizeInstallmentPlanJson
{
    public List<RequestInstallmentPlanRowVersionJson>? Installments { get; init; }

    /// <summary>
    /// Required to be true when the plan is Mixed (Draft installments alongside Active and/or Cancelled
    /// ones): the caller confirms activating exactly the remaining Draft installments.
    /// </summary>
    public bool ConfirmMixedPlan { get; init; }
}
