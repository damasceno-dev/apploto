namespace server.Communication.Responses;

/// <summary>Row counts by how their frozen classification was obtained (M9.7 Phase 1.3).</summary>
public class ResponseClassificationProvenanceJson
{
    /// <summary>Captured from the type when the row was posted.</summary>
    public int PostingCount { get; init; }

    /// <summary>Copied from the type at the M9.7 upgrade; the historical classification at posting is unknown.</summary>
    public int MigratedCount { get; init; }

    /// <summary>Changed by an explicit open-period reclassification.</summary>
    public int ReclassifiedCount { get; init; }
}
