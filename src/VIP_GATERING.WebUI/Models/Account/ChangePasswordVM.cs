using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.WebUI.Models.Account;

public class ChangePasswordVM
{
    [Required(ErrorMessage = "La contrasena actual es requerida")]
    [MinLength(20, ErrorMessage = "Debe tener al menos 20 caracteres")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contrasena es requerida")]
    [MinLength(20, ErrorMessage = "Debe tener al menos 20 caracteres")]
    [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[^\\da-zA-Z]).{20,}$", ErrorMessage = "Debe incluir mayusculas, minusculas, numero y simbolo.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmacion es requerida")]
    [Compare("NewPassword", ErrorMessage = "La confirmacion no coincide")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
