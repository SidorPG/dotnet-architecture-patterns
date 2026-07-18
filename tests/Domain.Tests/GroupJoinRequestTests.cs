using Domain.Abstractions;
using Domain.Aggregates.GroupJoinRequest;
using Domain.Aggregates.GroupJoinRequest.Events;
using Domain.Ids;
using Xunit;

namespace Domain.Tests;

public class GroupJoinRequestTests
{
    static readonly StudentId    StudentId    = new(Guid.NewGuid());
    static readonly GroupId      GroupId      = new(Guid.NewGuid());
    static readonly InstructorId InstructorId = new(Guid.NewGuid());

    // ── Create ────────────────────────────────────────────────────

    [Fact]
    public void Create_SetsPendingApprovalStatus()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);

        Assert.Equal(JoinRequestStatus.PendingApproval, req.Status);
        Assert.Equal(StudentId, req.StudentId);
        Assert.Equal(GroupId, req.GroupId);
    }

    [Fact]
    public void Create_RaisesGroupJoinRequestSubmittedEvent()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);

        var evt = Assert.Single(req.DomainEvents);
        Assert.IsType<GroupJoinRequestSubmitted>(evt);
    }

    // ── Accept ───────────────────────────────────────────────────

    [Fact]
    public void Accept_WhenPendingApproval_TransitionsToPendingPayment()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);
        req.PopDomainEvents();

        req.Accept(InstructorId, 500m, "EUR");

        Assert.Equal(JoinRequestStatus.PendingPayment, req.Status);
        Assert.Equal(500m, req.AgreedPrice);
        Assert.Equal("EUR", req.AgreedCurrency);
        Assert.Equal(InstructorId, req.ReviewedBy);
    }

    [Fact]
    public void Accept_WhenPendingApproval_RaisesAcceptedEvent()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);
        req.PopDomainEvents();

        req.Accept(InstructorId, 500m, "EUR");

        var evt = Assert.Single(req.DomainEvents);
        Assert.IsType<GroupJoinRequestAccepted>(evt);
    }

    [Fact]
    public void Accept_WhenAlreadyAccepted_ThrowsDomainException()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);
        req.Accept(InstructorId, 100m, "EUR");

        var ex = Assert.Throws<DomainException>(() => req.Accept(InstructorId, 200m, "EUR"));
        Assert.Contains("pending-approval", ex.Message);
    }

    [Fact]
    public void Accept_WhenPriceNegative_ThrowsDomainException()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);

        var ex = Assert.Throws<DomainException>(() => req.Accept(InstructorId, -1m, "EUR"));
        Assert.Contains("negative", ex.Message);
    }

    // ── Cancel ───────────────────────────────────────────────────

    [Fact]
    public void Cancel_WhenPendingApproval_TransitionsToCancelled()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);

        req.Cancel();

        Assert.Equal(JoinRequestStatus.Cancelled, req.Status);
    }

    [Fact]
    public void Cancel_WhenPendingPayment_TransitionsToCancelled()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);
        req.Accept(InstructorId, 100m, "EUR");

        req.Cancel();

        Assert.Equal(JoinRequestStatus.Cancelled, req.Status);
    }

    [Fact]
    public void Cancel_WhenConfirmed_ThrowsDomainException()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);
        req.Accept(InstructorId, 100m, "EUR");
        req.Confirm();

        Assert.Throws<DomainException>(() => req.Cancel());
    }

    // ── Reject ───────────────────────────────────────────────────

    [Fact]
    public void Reject_WhenPendingApproval_TransitionsToRejected()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);

        req.Reject(InstructorId);

        Assert.Equal(JoinRequestStatus.Rejected, req.Status);
        Assert.Equal(InstructorId, req.ReviewedBy);
    }

    [Fact]
    public void Reject_WhenPendingPayment_ThrowsDomainException()
    {
        var req = GroupJoinRequest.Create(StudentId, GroupId);
        req.Accept(InstructorId, 100m, "EUR");

        Assert.Throws<DomainException>(() => req.Reject(InstructorId));
    }
}
