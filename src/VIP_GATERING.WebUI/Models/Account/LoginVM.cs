using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.WebUI.Models.Account;

public class LoginVM
{
    [Required(ErrorMessage = "El usuario es requerido")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es requerida")]
    [MinLength(6, ErrorMessage = "La contrasena debe tener al menos 6 caracteres")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
