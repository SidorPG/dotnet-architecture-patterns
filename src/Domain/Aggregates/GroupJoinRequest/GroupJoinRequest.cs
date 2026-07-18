using Domain.Abstractions;
using Domain.Aggregates.GroupJoinRequest.Events;
using Domain.Ids;

namespace Domain.Aggregates.GroupJoinRequest;

public class GroupJoinRequest : AuditableEntity<GroupJoinRequestId>
{
    // Parameterless ctor required by EF Core — never called in business code.
    private GroupJoinRequest() : base(default) { }

    private GroupJoinRequest(
        GroupJoinRequestId id,
        StudentId studentId,
        GroupId groupId) : base(id)
    {
        StudentId   = studentId;
        GroupId     = groupId;
        Status      = JoinRequestStatus.PendingApproval;
        RequestedAt = DateTimeOffset.UtcNow;
    }

    public StudentId         StudentId        { get; private set; }
    public GroupId           GroupId          { get; private set; }
    public JoinRequestStatus Status           { get; private set; }
    public DateTimeOffset    RequestedAt      { get; private set; }
    public DateTimeOffset?   ReviewedAt       { get; private set; }
    public InstructorId?     ReviewedBy       { get; private set; }
    public decimal?          AgreedPrice      { get; private set; }
    public string?           AgreedCurrency   { get; private set; }

    public static GroupJoinRequest Create(StudentId studentId, GroupId groupId)
    {
        var req = new GroupJoinRequest(new GroupJoinRequestId(Guid.NewGuid()), studentId, groupId);
        req.RaiseDomainEvent(new GroupJoinRequestSubmitted(req.Id, studentId, groupId));
        return req;
    }

    public void Accept(InstructorId by, decimal agreedPrice, string agreedCurrency)
    {
        if (Status != JoinRequestStatus.PendingApproval)
            throw new DomainException("Only a pending-approval request can be accepted.");
        if (agreedPrice < 0)
            throw new DomainException("Agreed price cannot be negative.");

        Status         = JoinRequestStatus.PendingPayment;
        AgreedPrice    = agreedPrice;
        AgreedCurrency = agreedCurrency;
        ReviewedAt     = DateTimeOffset.UtcNow;
        ReviewedBy     = by;
        RaiseDomainEvent(new GroupJoinRequestAccepted(Id, StudentId, GroupId));
    }

    public void Confirm()
    {
        if (Status != JoinRequestStatus.PendingPayment)
            throw new DomainException("Only a pending-payment request can be confirmed.");

        Status = JoinRequestStatus.Confirmed;
        RaiseDomainEvent(new GroupJoinRequestConfirmed(Id, StudentId, GroupId));
    }

    public void Reject(InstructorId by)
    {
        if (Status != JoinRequestStatus.PendingApproval)
            throw new DomainException("Only a pending-approval request can be rejected.");

        Status     = JoinRequestStatus.Rejected;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewedBy = by;
        RaiseDomainEvent(new GroupJoinRequestRejected(Id, StudentId, GroupId));
    }

    public void Cancel()
    {
        if (Status is not (JoinRequestStatus.PendingApproval or JoinRequestStatus.PendingPayment))
            throw new DomainException("Only pending requests can be cancelled.");

        Status = JoinRequestStatus.Cancelled;
        RaiseDomainEvent(new GroupJoinRequestCancelled(Id, StudentId, GroupId));
    }
}
