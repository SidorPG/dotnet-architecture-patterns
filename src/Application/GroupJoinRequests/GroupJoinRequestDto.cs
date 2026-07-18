using Domain.Aggregates.GroupJoinRequest;

namespace Application.GroupJoinRequests;

public record GroupJoinRequestDto(
    Guid                Id,
    Guid                StudentId,
    Guid                GroupId,
    JoinRequestStatus   Status,
    DateTimeOffset      RequestedAt,
    decimal?            AgreedPrice,
    string?             AgreedCurrency
);
