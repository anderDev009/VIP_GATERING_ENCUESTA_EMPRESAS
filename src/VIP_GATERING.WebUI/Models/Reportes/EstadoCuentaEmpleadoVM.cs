namespace VIP_GATERING.WebUI.Models.Reportes;

public class EstadoCuentaEmpleadoVM
{
    public Guid EmpleadoId { get; set; }
    public string EmpleadoNombre { get; set; } = string.Empty;
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }

    public List<MovimientoRow> Movimientos { get; set; } = new();

    public decimal TotalConsumo => Movimientos.Sum(m => m.PrecioEmpleado);
    public int TotalSelecciones => Movimientos.Count;

    public class MovimientoRow
    {
        public DateOnly Fecha { get; set; }
        public string DiaNombre { get; set; } = string.Empty;
        public string? Horario { get; set; }
        public string Seleccion { get; set; } = string.Empty;
        public string? OpcionNombre { get; set; }
        public decimal PrecioEmpleado { get; set; }
    }
}
