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
        public required string Email { get; set; }

        [Required]
        [Phone]
        public required string PhoneNumber { get; set; }

        [Required]
        public required string Department { get; set; }

        [Required]
        public required string Position { get; set; }

        [Required]
        public required string BadgeNumber { get; set; }

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
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? BadgeNumber { get; set; }
        public bool IsApproved { get; set; }
    }
}