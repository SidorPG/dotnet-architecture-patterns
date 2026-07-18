namespace Domain.Aggregates.GroupJoinRequest;

public enum JoinRequestStatus
{
    PendingApproval,
    PendingPayment,
    Confirmed,
    Rejected,
    Cancelled,
}
