namespace VIP_GATERING.Domain.Entities;

/// <summary>
/// Configuración global para la ventana de edición del menú de empleados.
/// </summary>
public class MenuConfiguracion
{
    public int Id { get; set; }

    /// <summary>
    /// Permite o no la edición de la semana en curso.
    /// </summary>
    public bool PermitirEdicionSemanaActual { get; set; } = true;

    /// <summary>
    /// Cantidad de días de anticipación requeridos para editar (1 = día anterior).
    /// </summary>
    public int DiasAnticipoSemanaActual { get; set; } = 1;

    /// <summary>
    /// Hora límite del día anterior para permitir cambios.
    /// </summary>
    public TimeSpan HoraLimiteEdicion { get; set; } = new TimeSpan(12, 0, 0);

    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
}
