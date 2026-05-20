using System.ComponentModel.DataAnnotations;

namespace Api.DTOs;

public sealed class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
