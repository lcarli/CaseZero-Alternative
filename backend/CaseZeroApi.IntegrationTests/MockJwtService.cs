using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CaseZeroApi.Models;
using CaseZeroApi.Services;

namespace CaseZeroApi.IntegrationTests
{
    public class MockJwtService : IJwtService
    {
        // Using a fixed test secret key
        private const string TestSecretKey = "SuperSecretKeyForTestingPurposesOnly123456789";
        
        public string GenerateToken(User user, IEnumerable<string>? roles = null)
        {
            var key = Encoding.ASCII.GetBytes(TestSecretKey);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim("BadgeNumber", user.BadgeNumber ?? string.Empty),
                new Claim("Department", user.Department ?? string.Empty),
                new Claim("Position", user.Position ?? string.Empty)
            };
            claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = "CaseZeroTestIssuer",
                Audience = "CaseZeroTestAudience"
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        
        // Static method to get the test secret key for authentication configuration
        public static string GetTestSecretKey() => TestSecretKey;
    }
}
