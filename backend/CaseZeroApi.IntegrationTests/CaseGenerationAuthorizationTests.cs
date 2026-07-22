using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseZeroApi.DTOs;
using CaseZeroApi.Models;
using Xunit;

namespace CaseZeroApi.IntegrationTests;

public class CaseGenerationAuthorizationTests : IntegrationTestsBase
{
    public CaseGenerationAuthorizationTests(CustomWebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task Generate_PlayerRole_ReturnsForbidden()
    {
        var token = await CreateAuthenticatedUserAndGetToken(roles: [UserRoles.Player]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync(
            "/api/casegeneration/generate",
            CreateJsonContent(new { difficulty = "Rookie" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Generate_AdminRole_PassesRoleAuthorization()
    {
        var token = await CreateAuthenticatedUserAndGetToken(
            email: "admin@fic-police.gov",
            roles: [UserRoles.Player, UserRoles.Admin]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync(
            "/api/casegeneration/generate",
            CreateJsonContent(new { difficulty = "Rookie" }));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsRolesCarriedByTheJwt()
    {
        var token = await CreateAuthenticatedUserAndGetToken(
            email: "claims-admin@fic-police.gov",
            roles: [UserRoles.Player, UserRoles.Admin]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/auth/me");
        var user = await response.Content.ReadFromJsonAsync<UserDto>();

        response.EnsureSuccessStatusCode();
        Assert.NotNull(user);
        Assert.Contains(UserRoles.Player, user.Roles);
        Assert.Contains(UserRoles.Admin, user.Roles);
    }
}
