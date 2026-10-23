namespace WithSaga.Orchestrator.Models;

public enum SagaStatus
{
    Running,
    Completed,
    Compensated
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
