using System.ComponentModel.DataAnnotations;

namespace CaseZeroApi.DTOs
{
    public class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Password { get; set; }
    }

    public class RegisterRequestDto
    {
        [Required]
        public required string FirstName { get; set; }

        [Required]
        public required string LastName { get; set; }

        [Required]
        [EmailAddress]
        public required string PersonalEmail { get; set; }

        [Required]
        [MinLength(8)]
        public required string Password { get; set; }
    }

    public class LoginResponseDto
    {
        public required string Token { get; set; }
        public required UserDto User { get; set; }
    }

    public class UserDto
    {
        public required string Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string PersonalEmail { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? BadgeNumber { get; set; }
        public bool EmailVerified { get; set; }
    }

    public class VerifyEmailRequestDto
    {
        [Required]
        public required string Token { get; set; }
    }

    public class ResendVerificationRequestDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }
}