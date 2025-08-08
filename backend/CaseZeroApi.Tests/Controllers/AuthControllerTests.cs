using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using CaseZeroApi.Controllers;
using CaseZeroApi.Models;
using CaseZeroApi.DTOs;
using CaseZeroApi.Services;

namespace CaseZeroApi.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<User>> _mockUserManager;
        private readonly Mock<SignInManager<User>> _mockSignInManager;
        private readonly Mock<IJwtService> _mockJwtService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            var store = new Mock<IUserStore<User>>();
            _mockUserManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            
            var contextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimsFactory = new Mock<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<User>>();
            _mockSignInManager = new Mock<SignInManager<User>>(_mockUserManager.Object, contextAccessor.Object, claimsFactory.Object, null!, null!, null!, null!);
            
            _mockJwtService = new Mock<IJwtService>();
            _mockEmailService = new Mock<IEmailService>();
            _mockLogger = new Mock<ILogger<AuthController>>();

            _controller = new AuthController(
                _mockUserManager.Object,
                _mockSignInManager.Object,
                _mockJwtService.Object,
                _mockEmailService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task Register_ValidRequest_ReturnsSuccessResponse()
        {
            // Arrange
            var request = new RegisterRequestDto
            {
                FirstName = "John",
                LastName = "Doe",
                PersonalEmail = "john.doe@personal.com",
                Password = "Password123!"
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            _mockEmailService.Setup(x => x.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult);
        }

        [Fact]
        public async Task Register_UserAlreadyExists_ReturnsBadRequest()
        {
            // Arrange
            var request = new RegisterRequestDto
            {
                FirstName = "John",
                LastName = "Doe",
                PersonalEmail = "john.doe@personal.com",
                Password = "Password123!"
            };

            var existingUser = new User 
            { 
                Email = "john.doe@fic-police.gov",
                FirstName = "John",
                LastName = "Doe",
                PersonalEmail = "john.doe@personal.com"
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(existingUser);

            // Act
            var result = await _controller.Register(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsTokenResponse()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "john.doe@fic-police.gov",
                Password = "Password123!"
            };

            var user = new User 
            { 
                Id = "test-id",
                Email = "john.doe@fic-police.gov",
                FirstName = "John",
                LastName = "Doe",
                PersonalEmail = "john.doe@personal.com",
                EmailConfirmed = true
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync(user);
            
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            _mockJwtService.Setup(x => x.GenerateToken(user))
                .Returns("test-jwt-token");

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequestDto
            {
                Email = "invalid@fic-police.gov",
                Password = "WrongPassword"
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _controller.Login(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}