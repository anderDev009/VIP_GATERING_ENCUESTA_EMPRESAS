namespace VIP_GATERING.WebUI.Models.Reportes;

public class EstadoCuentaEmpleadoVM
{
    public int EmpleadoId { get; set; }
    public string EmpleadoNombre { get; set; } = string.Empty;
    public string? EmpleadoCodigo { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }

    public List<MovimientoRow> Movimientos { get; set; } = new();

    public decimal TotalBase => Movimientos.Sum(m => m.BaseEmpleado);
    public decimal TotalItbis => Movimientos.Sum(m => m.ItbisEmpleado);
    public decimal TotalConsumo => Movimientos.Sum(m => m.TotalEmpleado);
    public int TotalSelecciones => Movimientos.Count;

    public class MovimientoRow
    {
        public DateOnly Fecha { get; set; }
        public string DiaNombre { get; set; } = string.Empty;
        public string? Horario { get; set; }
        public string Seleccion { get; set; } = string.Empty;
        public string? OpcionNombre { get; set; }
        public decimal BaseEmpleado { get; set; }
        public decimal ItbisEmpleado { get; set; }
        public decimal TotalEmpleado { get; set; }
    }
}
