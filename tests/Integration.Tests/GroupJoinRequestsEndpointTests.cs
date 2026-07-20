using Application.Common.Authorization;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// End-to-end tests against a real PostgreSQL container.
/// Each test boots the full ASP.NET Core pipeline, runs EF migrations,
/// and exercises HTTP endpoints — no mocks anywhere in the call stack.
/// </summary>
public class GroupJoinRequestsEndpointTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    // Client pre-authorized with all permissions — used for the happy-path tests.
    private readonly HttpClient _client;

    public GroupJoinRequestsEndpointTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClientWithPermissions(
            Permissions.JoinRequests.Read,
            Permissions.JoinRequests.StudentWrite,
            Permissions.JoinRequests.InstructorWrite);
    }

    // ── DTOs for response deserialization ────────────────────────────
    private record SubmitResultDto(Guid Id);
    private record GroupJoinRequestDto(
        Guid            Id,
        Guid            StudentId,
        Guid            GroupId,
        string          Status,
        DateTimeOffset  RequestedAt,
        decimal?        AgreedPrice,
        string?         AgreedCurrency);

    // ── Happy-path tests ──────────────────────────────────────────────

    [Fact]
    public async Task Submit_Returns201_WithNewId()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/group-join-requests", new
        {
            studentId = Guid.NewGuid(),
            groupId   = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SubmitResultDto>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task GetById_AfterSubmit_ReturnsPendingApproval()
    {
        var id = await SubmitRequest();

        var response = await _client.GetAsync($"/api/v1/group-join-requests/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<GroupJoinRequestDto>();
        Assert.NotNull(dto);
        Assert.Equal(id, dto.Id);
        Assert.Equal("PendingApproval", dto.Status);
        Assert.Null(dto.AgreedPrice);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/group-join-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPending_ContainsSubmittedRequest()
    {
        var id = await SubmitRequest();

        var response = await _client.GetAsync("/api/v1/group-join-requests");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<GroupJoinRequestDto[]>();
        Assert.NotNull(list);
        Assert.Contains(list, r => r.Id == id);
    }

    [Fact]
    public async Task Accept_TransitionsStatus_ToPendingPayment()
    {
        var id = await SubmitRequest();

        var acceptResponse = await _client.PostAsJsonAsync(
            $"/api/v1/group-join-requests/{id}/accept",
            new { agreedPrice = 599.00m, agreedCurrency = "EUR" });

        Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);

        var dto = await GetRequest(id);
        Assert.Equal("PendingPayment", dto.Status);
        Assert.Equal(599.00m, dto.AgreedPrice);
        Assert.Equal("EUR", dto.AgreedCurrency);
    }

    [Fact]
    public async Task Accept_AlreadyAccepted_Returns400()
    {
        var id = await SubmitRequest();

        await _client.PostAsJsonAsync(
            $"/api/v1/group-join-requests/{id}/accept",
            new { agreedPrice = 100m, agreedCurrency = "EUR" });

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/group-join-requests/{id}/accept",
            new { agreedPrice = 200m, agreedCurrency = "USD" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Accept_UnknownId_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/group-join-requests/{Guid.NewGuid()}/accept",
            new { agreedPrice = 100m, agreedCurrency = "EUR" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Authorization tests ───────────────────────────────────────────

    [Fact]
    public async Task AnyEndpoint_WithoutToken_Returns401()
    {
        var anonymous = _factory.CreateClient(); // no Authorization header

        var response = await anonymous.GetAsync("/api/v1/group-join-requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPending_WithStudentPermissionOnly_Returns403()
    {
        var student = _factory.CreateClientWithPermissions(Permissions.JoinRequests.StudentWrite);

        var response = await student.GetAsync("/api/v1/group-join-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Submit_WithInstructorPermissionOnly_Returns403()
    {
        var instructor = _factory.CreateClientWithPermissions(Permissions.JoinRequests.InstructorWrite);

        var response = await instructor.PostAsJsonAsync("/api/v1/group-join-requests", new
        {
            studentId = Guid.NewGuid(),
            groupId   = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private async Task<Guid> SubmitRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/group-join-requests", new
        {
            studentId = Guid.NewGuid(),
            groupId   = Guid.NewGuid()
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SubmitResultDto>();
        return result!.Id;
    }

    private async Task<GroupJoinRequestDto> GetRequest(Guid id)
    {
        var response = await _client.GetAsync($"/api/v1/group-join-requests/{id}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupJoinRequestDto>())!;
    }
}
