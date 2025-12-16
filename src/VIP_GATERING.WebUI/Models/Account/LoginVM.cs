using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.WebUI.Models.Account;

public class LoginVM
{
    [Required(ErrorMessage = "El correo es requerido")]
    [EmailAddress(ErrorMessage = "Formato de correo inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
