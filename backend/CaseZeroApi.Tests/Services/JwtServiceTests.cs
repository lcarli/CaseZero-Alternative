using System.IdentityModel.Tokens.Jwt;
using CaseZeroApi.Models;
using CaseZeroApi.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CaseZeroApi.Tests.Services;

public class JwtServiceTests
{
    [Fact]
    public void GenerateToken_IncludesEveryAssignedRole()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "TestSecretKeyWithAtLeastThirtyTwoCharacters!",
                ["JwtSettings:Issuer"] = "test-issuer",
                ["JwtSettings:Audience"] = "test-audience"
            })
            .Build();
        var user = new User
        {
            Id = "user-1",
            UserName = "lucasadmin@fic-police.gov",
            Email = "lucasadmin@fic-police.gov",
            FirstName = "Lucas",
            LastName = "Admin",
            PersonalEmail = "lucasadmin@fic-police.gov"
        };

        var token = new JwtService(configuration).GenerateToken(
            user,
            [UserRoles.Player, UserRoles.Admin]);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims;

        Assert.Contains(claims, claim => claim.Type == "role" && claim.Value == UserRoles.Player);
        Assert.Contains(claims, claim => claim.Type == "role" && claim.Value == UserRoles.Admin);
    }
}
