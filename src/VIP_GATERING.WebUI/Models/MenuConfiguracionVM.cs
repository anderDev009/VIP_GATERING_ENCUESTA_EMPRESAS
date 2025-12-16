using System.ComponentModel.DataAnnotations;

namespace VIP_GATERING.WebUI.Models;

public class MenuConfiguracionVM
{
    [Display(Name = "Permitir edicion semana actual")]
    public bool PermitirEdicionSemanaActual { get; set; } = true;

    [Range(0, 7, ErrorMessage = "Debe estar entre 0 y 7 dias.")]
    public int DiasAnticipoSemanaActual { get; set; } = 1;

    [Required]
    [Display(Name = "Hora limite (HH:mm)")]
    public string HoraLimite { get; set; } = "12:00";
}
