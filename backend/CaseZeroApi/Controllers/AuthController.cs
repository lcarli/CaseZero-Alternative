using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CaseZeroApi.Models;
using CaseZeroApi.DTOs;
using CaseZeroApi.Services;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtService jwtService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new User
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Department = request.Department,
                Position = request.Position,
                BadgeNumber = request.BadgeNumber,
                IsApproved = false // Requires admin approval
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} registered successfully", request.Email);
                return Ok(new { Message = "Registration successful. Awaiting admin approval." });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return BadRequest(ModelState);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new { Message = "Invalid credentials" });
            }

            if (!user.IsApproved)
            {
                return Unauthorized(new { Message = "Account not approved yet. Please contact administrator." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (result.Succeeded)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                var token = _jwtService.GenerateToken(user);
                var userDto = new UserDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email!,
                    Department = user.Department,
                    Position = user.Position,
                    BadgeNumber = user.BadgeNumber,
                    IsApproved = user.IsApproved
                };

                _logger.LogInformation("User {Email} logged in successfully", request.Email);
                return Ok(new LoginResponseDto { Token = token, User = userDto });
            }

            return Unauthorized(new { Message = "Invalid credentials" });
        }
    }
}