namespace PurviewConsortium.Core.Interfaces;

/// <summary>
/// Result of submitting a data access workflow request to Purview.
/// </summary>
public class WorkflowSubmitResult
{
    public bool Success { get; set; }
    public string? WorkflowRunId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DataMapAssetGuid { get; set; }
}

/// <summary>
/// Result of checking a Purview workflow run's status.
/// </summary>
public class WorkflowRunStatusResult
{
    /// <summary>Whether the API call succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Purview workflow run status: InProgress, Completed, Canceling, Canceled, Failed.</summary>
    public string? RunStatus { get; set; }

    /// <summary>If the approval action completed, whether it was Approved or Rejected.</summary>
    public string? ApprovalOutcome { get; set; }

    /// <summary>Error message if the API call failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Submits self-service data access requests to the Purview Workflow API,
/// enabling Purview's built-in approval workflows to handle access granting.
/// </summary>
public interface IPurviewWorkflowService
{
    /// <summary>
    /// Submits a GrantDataAccess workflow request to the specified Purview account.
    /// Searches the DataMap for an asset matching the given name, then triggers
    /// the self-service access workflow.
    /// </summary>
    Task<WorkflowSubmitResult> SubmitAccessRequestAsync(
        string purviewAccountName,
        string tenantId,
        string dataProductName,
        string businessJustification,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the current status of a Purview workflow run.
    /// Returns the run status and, if the workflow completed, the approval outcome.
    /// </summary>
    Task<WorkflowRunStatusResult> GetWorkflowRunStatusAsync(
        string purviewAccountName,
        string tenantId,
        string workflowRunId,
        string? userAccessToken = null,
        CancellationToken cancellationToken = default);
}
