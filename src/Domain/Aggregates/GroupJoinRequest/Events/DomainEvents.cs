using Domain.Abstractions;
using Domain.Ids;

namespace Domain.Aggregates.GroupJoinRequest.Events;

public record GroupJoinRequestSubmitted(GroupJoinRequestId RequestId, StudentId StudentId, GroupId GroupId) : IDomainEvent;
public record GroupJoinRequestAccepted (GroupJoinRequestId RequestId, StudentId StudentId, GroupId GroupId) : IDomainEvent;
public record GroupJoinRequestConfirmed(GroupJoinRequestId RequestId, StudentId StudentId, GroupId GroupId) : IDomainEvent;
public record GroupJoinRequestRejected (GroupJoinRequestId RequestId, StudentId StudentId, GroupId GroupId) : IDomainEvent;
public record GroupJoinRequestCancelled(GroupJoinRequestId RequestId, StudentId StudentId, GroupId GroupId) : IDomainEvent;
