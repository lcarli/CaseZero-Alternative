using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CaseZeroApi.Models;
using CaseZeroApi.DTOs;
using CaseZeroApi.Services;
using CaseZeroApi.Data;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace CaseZeroApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtService _jwtService;
        // OBSOLETE: Email service removed (was from old email system)
        // private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger;
        private readonly ApplicationDbContext _db;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtService jwtService,
            // IEmailService emailService,
            ILogger<AuthController> logger,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            // _emailService = emailService;
            _logger = logger;
            _db = db;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Generate police email from first and last name
            var policeEmail = GeneratePoliceEmail(request.FirstName, request.LastName);
            
            // Check if police email already exists
            var existingUser = await _userManager.FindByEmailAsync(policeEmail);
            if (existingUser != null)
            {
                return BadRequest(new { Message = "Um usuário com este nome já existe no sistema." });
            }

            // Generate unique badge number
            var badgeNumber = await GenerateUniqueBadgeNumberAsync();
            
            // Generate email verification token
            var verificationToken = GenerateEmailVerificationToken();

            var user = new User
            {
                UserName = policeEmail,
                Email = policeEmail,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PersonalEmail = request.PersonalEmail,
                Department = "ColdCase", // Auto-set as required
                Position = "rook",       // Auto-set as required
                BadgeNumber = badgeNumber,
                EmailVerified = false,
                EmailVerificationToken = verificationToken,
                EmailVerificationSentAt = DateTime.UtcNow,
                Rank = DetectiveRank.Rook // Auto-set as required
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (result.Succeeded)
            {
                var roleResult = await _userManager.AddToRoleAsync(user, UserRoles.Player);
                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(user);
                    foreach (var error in roleResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return StatusCode(500, ModelState);
                }

                // TODO: Send verification email to personal email (IEmailService removed)
                // Email functionality was part of old system - needs reimplementation
                _logger.LogInformation("User {Email} registered successfully. Auto-verified (email service disabled).", 
                    policeEmail);
                
                // Auto-verify for now since email service is obsolete
                user.EmailVerified = true;
                await _userManager.UpdateAsync(user);

                // Seed the initial rank-history row so the profile timeline has a starting point.
                _db.UserRankHistories.Add(new UserRankHistory
                {
                    UserId = user.Id,
                    PreviousRank = null,
                    NewRank = user.Rank,
                    ChangedAt = DateTime.UtcNow,
                    Reason = "Account created"
                });
                await _db.SaveChangesAsync();
                
                return Ok(new { 
                    Message = "Registro realizado com sucesso! Conta ativada automaticamente.",
                    PoliceEmail = policeEmail,
                    PersonalEmail = request.PersonalEmail
                });
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

            if (!user.EmailVerified)
            {
                return Unauthorized(new { Message = "Email não verificado. Verifique seu email pessoal para ativar a conta." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (result.Succeeded)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                var roles = await _userManager.GetRolesAsync(user);
                var token = _jwtService.GenerateToken(user, roles);
                var userDto = ToDto(user, roles);

                _logger.LogInformation("User {Email} logged in successfully", request.Email);
                return Ok(new LoginResponseDto { Token = token, User = userDto });
            }

            return Unauthorized(new { Message = "Invalid credentials" });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized();

            var roles = User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return Ok(ToDto(user, roles));
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == request.Token);

            if (user == null)
            {
                return BadRequest(new { Message = "Token de verificação inválido." });
            }

            if (user.EmailVerificationSentAt == null || 
                DateTime.UtcNow.Subtract(user.EmailVerificationSentAt.Value).TotalHours > 24)
            {
                return BadRequest(new { Message = "Token de verificação expirado." });
            }

            if (user.EmailVerified)
            {
                return BadRequest(new { Message = "Email já verificado." });
            }

            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationSentAt = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return StatusCode(500, new { Message = "Erro ao verificar email." });
            }

            // Send welcome email
            // TODO: Send welcome email (IEmailService removed)
            _logger.LogInformation("User {Email} verified email successfully (welcome email disabled)", user.Email);

            _logger.LogInformation("User {Email} verified email successfully", user.Email);
            return Ok(new { Message = "Email verificado com sucesso! Sua conta está ativa." });
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || user.EmailVerified)
            {
                // Don't reveal whether user exists or is already verified
                return Ok(new { Message = "Se o email existe e não foi verificado, um novo email foi enviado." });
            }

            // Check if we can resend (limit to prevent spam)
            if (user.EmailVerificationSentAt != null && 
                DateTime.UtcNow.Subtract(user.EmailVerificationSentAt.Value).TotalMinutes < 5)
            {
                return BadRequest(new { Message = "Aguarde 5 minutos antes de solicitar um novo email." });
            }

            // Generate new verification token
            var verificationToken = GenerateEmailVerificationToken();
            user.EmailVerificationToken = verificationToken;
            user.EmailVerificationSentAt = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return StatusCode(500, new { Message = "Erro ao reenviar email." });
            }

            // TODO: Send verification email (IEmailService removed)
            _logger.LogInformation("Verification email resend requested for user {Email} (email service disabled)", user.Email);
            return Ok(new { Message = "Funcionalidade de email temporariamente desabilitada. Conta auto-verificada." });
        }

        private string GeneratePoliceEmail(string firstName, string lastName)
        {
            // Remove accents and special characters, convert to lowercase
            var cleanFirstName = RemoveAccents(firstName.Trim().Split(' ')[0]).ToLowerInvariant();
            var cleanLastName = RemoveAccents(lastName.Trim().Split(' ').LastOrDefault() ?? "").ToLowerInvariant();
            
            return $"{cleanFirstName}.{cleanLastName}@fic-police.gov";
        }

        private string RemoveAccents(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private async Task<string> GenerateUniqueBadgeNumberAsync()
        {
            string badgeNumber;
            bool isUnique;

            do
            {
                // Generate 4-digit badge number
                badgeNumber = Random.Shared.Next(1000, 9999).ToString();
                
                // Check if it's unique by getting all users and checking in memory
                // This is acceptable as badge numbers are checked during registration only
                var allUsers = _userManager.Users.ToList();
                isUnique = !allUsers.Any(u => u.BadgeNumber == badgeNumber);
                
            } while (!isUnique);

            return badgeNumber;
        }

        private string GenerateEmailVerificationToken()
        {
            // Generate a secure random token
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
            }
        }

        private static UserDto ToDto(User user, IEnumerable<string> roles) => new()
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email!,
            PersonalEmail = user.PersonalEmail,
            Department = user.Department,
            Position = user.Position,
            BadgeNumber = user.BadgeNumber,
            EmailVerified = user.EmailVerified,
            Roles = roles.ToArray()
        };
    }
}