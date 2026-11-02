namespace WithSaga.Orchestrator.Models;

public enum SagaStatus
{
    Running,      // In progress, or it could not reach a clean end
    Completed,    // Every step succeeded
    Compensated   // A step failed and every compensation succeeded
}

/// <summary>
/// One saga row in the orchestrator database.
/// </summary>
public class SagaInstance
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public SagaStatus Status { get; set; }
}
