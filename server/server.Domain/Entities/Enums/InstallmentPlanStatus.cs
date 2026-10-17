namespace server.Domain.Entities.Enums;

public enum InstallmentPlanStatus
{
    Draft = 0,      // Every installment is Draft
    Active = 1,     // No Draft installment remains; at least one is Active
    Mixed = 2,      // Draft installments alongside Active and/or Cancelled ones
    Cancelled = 3   // Every installment is Cancelled
}
