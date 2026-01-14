using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace VIP_GATERING.WebUI.Models;

public class MenuImportVM
{
    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }
    public int? EmpresaId { get; set; }
    public IFormFile? Archivo { get; set; }

    public int TotalFilas { get; set; }
    public int FilasProcesadas { get; set; }
    public int EmpleadosCreados { get; set; }
    public int UsuariosCreados { get; set; }
    public int SeleccionesGuardadas { get; set; }
    public int SeleccionesSaltadas { get; set; }

    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}
