using API.Swagger;
using Application.Common.Interfaces;
using Application.GroupJoinRequests;
using Domain.Ids;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcceptCommand = Application.GroupJoinRequests.AcceptGroupJoinRequest.Command;
using GetByIdQuery = Application.GroupJoinRequests.GetGroupJoinRequest.Query;
using GetPendingQuery = Application.GroupJoinRequests.GetPendingGroupJoinRequests.Query;
using SubmitCommand = Application.GroupJoinRequests.SubmitGroupJoinRequest.Command;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/group-join-requests")]
public class GroupJoinRequestsController : ControllerBase
{
    private readonly ISender      _sender;
    private readonly ICurrentUser _currentUser;

    public GroupJoinRequestsController(ISender sender, ICurrentUser currentUser)
    {
        _sender      = sender;
        _currentUser = currentUser;
    }

    /// <summary>Returns all pending-approval join requests.</summary>
    /// <response code="200">List of pending requests (may be empty).</response>
    [HttpGet]
    [RequiredPermission(Application.Common.Authorization.Permissions.JoinRequests.InstructorWrite)]
    [ProducesResponseType(typeof(IReadOnlyList<GroupJoinRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var result = await _sender.Send(new GetPendingQuery(), ct);
        return Ok(result.Value);
    }

    /// <summary>Gets a single join request by ID.</summary>
    /// <response code="200">Request found.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Request not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GroupJoinRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetByIdQuery(new GroupJoinRequestId(id)), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    /// <summary>Submits a new group join request.</summary>
    /// <response code="201">Created — returns the new request ID.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost]
    [RequiredPermission(Application.Common.Authorization.Permissions.JoinRequests.StudentWrite)]
    [ProducesResponseType(typeof(SubmitResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] SubmitBody body, CancellationToken ct)
    {
        var command = new SubmitCommand(new StudentId(body.StudentId), new GroupId(body.GroupId));
        var result  = await _sender.Send(command, ct);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Value },
            new SubmitResult(result.Value.Value));
    }

    /// <summary>
    /// Accepts a pending join request. Instructor permission required.
    /// The acting instructor is resolved from the JWT sub claim.
    /// </summary>
    /// <response code="204">Accepted successfully.</response>
    /// <response code="400">Validation error or domain guard violation.</response>
    /// <response code="404">Request not found.</response>
    [HttpPost("{id:guid}/accept")]
    [RequiredPermission(Application.Common.Authorization.Permissions.JoinRequests.InstructorWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(Guid id, [FromBody] AcceptBody body, CancellationToken ct)
    {
        var instructorId = new InstructorId(_currentUser.UserId ?? Guid.Empty);
        var command = new AcceptCommand(
            new GroupJoinRequestId(id),
            instructorId,
            body.AgreedPrice,
            body.AgreedCurrency);

        var result = await _sender.Send(command, ct);

        if (!result.IsSuccess)
            return result.IsNotFound ? NotFound() : BadRequest(new { error = result.Error });

        return NoContent();
    }

    public record SubmitBody(Guid StudentId, Guid GroupId);
    public record SubmitResult(Guid Id);
    public record AcceptBody(decimal AgreedPrice, string AgreedCurrency);
}
