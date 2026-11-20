using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>Details a new user supplies when registering (Part 1 plan: role, firstName, lastName, email, password, phoneNumber).</summary>
public class RegisterRequest
{
    /// <summary>Organiser or Participant.</summary>
    [Required]
    public UserRole? Role { get; set; }

    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Email address; used to log in and must be unique.</summary>
    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    /// <summary>8-100 characters with at least one letter and one digit.</summary>
    [Required, RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,100}$",
        ErrorMessage = "Password must be 8-100 characters and contain at least one letter and one digit.")]
    public string Password { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Required only when registering as an Organiser. Organiser accounts can create and manage events,
    /// so they are gated by an access code issued by the RaceDay administrators.
    /// </summary>
    public string? OrganiserAccessCode { get; set; }
}

/// <summary>Credentials used to log in.</summary>
public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Profile fields a user may change (Part 1 plan: firstName, lastName, phoneNumber).</summary>
public class UpdateProfileRequest
{
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }
}

/// <summary>A user object. Never includes the password or its hash.</summary>
public class UserResponse
{
    public int UserId { get; set; }
    public UserRole Role { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public static UserResponse From(AppUser user) => new()
    {
        UserId = user.UserId,
        Role = user.Role,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        ProfileImageUrl = user.ProfileImageUrl,
        CreatedAt = user.CreatedAt
    };
}
