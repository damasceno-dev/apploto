namespace server.Application.Services.DailyCloses;

/// <summary>
/// Defines the automated workflow state transitions triggered when 
/// editing items in a daily closing session.
/// </summary>
public enum DailyCloseEditItemsOutcome
{
     /// <summary>
     /// Editing a session already in Draft; no status change required.
     /// </summary>
     EditOnDraft = 0,

     /// <summary>
     /// Editing a session in the Rejected state; the session is automatically 
     /// transitioned back to Draft to allow resubmission.
     /// </summary>
     EditOnRejectedAutoTransitionToDraft = 1
}
