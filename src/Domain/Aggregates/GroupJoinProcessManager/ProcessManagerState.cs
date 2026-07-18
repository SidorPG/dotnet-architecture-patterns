namespace Domain.Aggregates.GroupJoinProcessManager;

public enum ProcessManagerState
{
    Created,
    AwaitingPayment,
    Completing,
    Completed,
    Compensating,
    Compensated,
}
