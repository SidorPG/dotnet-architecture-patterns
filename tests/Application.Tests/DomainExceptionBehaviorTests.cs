using Application.Common.Behaviors;
using Domain.Abstractions;
using MediatR;
using Moq;
using Xunit;

namespace Application.Tests;

public class DomainExceptionBehaviorTests
{
    record TestCommand : IRequest<Result>;
    record TestCommandWithValue : IRequest<Result<string>>;

    // ── Result (non-generic) ──────────────────────────────────────

    [Fact]
    public async Task Handle_WhenDomainExceptionThrown_ReturnsFailure_ForResult()
    {
        var behavior = new DomainExceptionBehavior<TestCommand, Result>();
        var command  = new TestCommand();

        RequestHandlerDelegate<Result> next = () =>
            throw new DomainException("price cannot be negative");

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("price cannot be negative", result.Error);
    }

    [Fact]
    public async Task Handle_WhenNoDomainException_ReturnsDelegateResult()
    {
        var behavior = new DomainExceptionBehavior<TestCommand, Result>();
        var command  = new TestCommand();
        var expected = Result.Success();

        RequestHandlerDelegate<Result> next = () => Task.FromResult(expected);

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // ── Result<T> (generic) ───────────────────────────────────────

    [Fact]
    public async Task Handle_WhenDomainExceptionThrown_ReturnsFailure_ForResultOfT()
    {
        var behavior = new DomainExceptionBehavior<TestCommandWithValue, Result<string>>();
        var command  = new TestCommandWithValue();

        RequestHandlerDelegate<Result<string>> next = () =>
            throw new DomainException("invariant violated");

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("invariant violated", result.Error);
    }

    [Fact]
    public async Task Handle_WhenNonDomainExceptionThrown_Rethrows()
    {
        var behavior = new DomainExceptionBehavior<TestCommand, Result>();
        var command  = new TestCommand();

        RequestHandlerDelegate<Result> next = () =>
            throw new InvalidOperationException("unexpected");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(command, next, CancellationToken.None));
    }

    // ── GetGroupJoinRequest query handler ─────────────────────────

    [Fact]
    public async Task GetGroupJoinRequest_WhenNotFound_ReturnsNotFound()
    {
        var mockQueryService = new Mock<Application.GroupJoinRequests.IGroupJoinRequestQueryService>();
        mockQueryService
            .Setup(s => s.GetByIdAsync(It.IsAny<Domain.Ids.GroupJoinRequestId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application.GroupJoinRequests.GroupJoinRequestDto?)null);

        var handler = new Application.GroupJoinRequests.GetGroupJoinRequest.Handler(mockQueryService.Object);
        var query   = new Application.GroupJoinRequests.GetGroupJoinRequest.Query(
            new Domain.Ids.GroupJoinRequestId(Guid.NewGuid()));

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Not Found", result.Error);
    }

    [Fact]
    public async Task GetGroupJoinRequest_WhenFound_ReturnsDto()
    {
        var id  = new Domain.Ids.GroupJoinRequestId(Guid.NewGuid());
        var dto = new Application.GroupJoinRequests.GroupJoinRequestDto(
            id.Value, Guid.NewGuid(), Guid.NewGuid(),
            Domain.Aggregates.GroupJoinRequest.JoinRequestStatus.PendingApproval,
            DateTimeOffset.UtcNow, null, null);

        var mockQueryService = new Mock<Application.GroupJoinRequests.IGroupJoinRequestQueryService>();
        mockQueryService
            .Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var handler = new Application.GroupJoinRequests.GetGroupJoinRequest.Handler(mockQueryService.Object);
        var result  = await handler.Handle(
            new Application.GroupJoinRequests.GetGroupJoinRequest.Query(id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(id.Value, result.Value.Id);
    }
}
