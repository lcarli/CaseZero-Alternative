using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using CaseZeroApi.DTOs;

namespace CaseZeroApi.IntegrationTests
{
    public class AuthIntegrationTests : IntegrationTestsBase
    {
        public AuthIntegrationTests(CustomWebApplicationFactory<Program> factory) : base(factory)
        {
        }

        [Fact]
        public async Task Register_ValidUser_ReturnsSuccess()
        {
            // Arrange
            var registerRequest = new RegisterRequestDto
            {
                FirstName = "Integration",
                LastName = "Test",
                PersonalEmail = "integration.test@email.com",
                Password = "Password123!"
            };

            // Act
            var response = await _client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_DuplicateUser_ReturnsBadRequest()
        {
            // Arrange
            var registerRequest = new RegisterRequestDto
            {
                FirstName = "Duplicate",
                LastName = "User",
                PersonalEmail = "duplicate.user@email.com",
                Password = "Password123!"
            };

            // Act - First registration
            await _client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));
            
            // Act - Second registration with same data
            var response = await _client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new LoginRequestDto
            {
                Email = "nonexistent@fic-police.gov",
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsync("/api/auth/login", CreateJsonContent(loginRequest));

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}