using Domain.Aggregates.GroupJoinProcessManager;
using Domain.Ids;

namespace Application.GroupJoinProcessManagers;

// Infrastructure implements this — Application declares the contract.
public interface IGroupJoinProcessManagerQueryService
{
    Task<GroupJoinProcessManager?> FindByPaymentIdAsync(PaymentId paymentId, CancellationToken ct);
}
