namespace server.Application.Services.DailyCloses;

public enum DailyCloseEditItemsOutcome
{
    EditOnDraft = 0,
    EditOnRejectedAutoTransitionToDraft = 1,
    EditOnSubmittedRecallToDraft = 2
}
