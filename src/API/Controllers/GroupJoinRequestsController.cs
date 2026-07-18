using Application.Common.Interfaces;
using Application.GroupJoinRequests;
using Application.GroupJoinRequests.AcceptGroupJoinRequest;
using Application.GroupJoinRequests.GetGroupJoinRequest;
using Domain.Ids;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
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
        var result = await _sender.Send(new Query(new GroupJoinRequestId(id)), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    /// <summary>
    /// Accepts a pending join request. Instructor permission required.
    /// The acting instructor is resolved from the JWT sub claim.
    /// </summary>
    /// <response code="204">Accepted successfully.</response>
    /// <response code="400">Validation error or domain guard violation.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Missing instructor permission.</response>
    /// <response code="404">Request not found.</response>
    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(Guid id, [FromBody] AcceptBody body, CancellationToken ct)
    {
        var instructorId = new InstructorId(_currentUser.UserId ?? Guid.Empty);
        var command = new Command(
            new GroupJoinRequestId(id),
            instructorId,
            body.AgreedPrice,
            body.AgreedCurrency);

        var result = await _sender.Send(command, ct);

        if (!result.IsSuccess)
            return result.IsNotFound ? NotFound() : BadRequest(new { error = result.Error });

        return NoContent();
    }

    public record AcceptBody(decimal AgreedPrice, string AgreedCurrency);
}
