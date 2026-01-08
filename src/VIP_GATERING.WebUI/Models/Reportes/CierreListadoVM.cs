using VIP_GATERING.WebUI.Models;

namespace VIP_GATERING.WebUI.Models.Reportes;

public class CierreListadoVM
{
    public string Titulo { get; set; } = string.Empty;
    public bool MostrarCrear { get; set; }
    public string AccionCrear { get; set; } = string.Empty;
    public string AccionEditar { get; set; } = string.Empty;
    public string AccionDetalle { get; set; } = string.Empty;
    public string AccionEliminar { get; set; } = string.Empty;
    public PagedResult<Row> Paginado { get; set; } = new();

    public class Row
    {
        public int EmpresaId { get; set; }
        public int SucursalId { get; set; }
        public string Empresa { get; set; } = string.Empty;
        public string Filial { get; set; } = string.Empty;
        public DateOnly Inicio { get; set; }
        public DateOnly Fin { get; set; }
        public int TotalSelecciones { get; set; }
        public bool Cerrado { get; set; }
        public string Estado => Cerrado ? "Cerrado" : "Abierto";
    }
}
