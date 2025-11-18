using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.WebUI.Models.Account;

public class ChangePasswordVM
{
    [Required(ErrorMessage = "La contraseña actual es requerida")]
    [MinLength(3, ErrorMessage = "Debe tener al menos 3 caracteres")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es requerida")]
    [MinLength(3, ErrorMessage = "Debe tener al menos 3 caracteres")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmación es requerida")]
    [Compare("NewPassword", ErrorMessage = "La confirmación no coincide")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

