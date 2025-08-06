using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CaseZeroApi.Models;

namespace CaseZeroApi.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"));
            
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim("BadgeNumber", user.BadgeNumber ?? string.Empty),
                new Claim("Department", user.Department ?? string.Empty),
                new Claim("Position", user.Position ?? string.Empty)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7), // Token valid for 7 days
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}