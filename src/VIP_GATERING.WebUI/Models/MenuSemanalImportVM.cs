using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace VIP_GATERING.WebUI.Models;

public class MenuSemanalImportVM
{
    public DateOnly Inicio { get; set; }
    public DateOnly Fin { get; set; }
    public int? EmpresaId { get; set; }
    public int? SucursalId { get; set; }
    public IFormFile? Archivo { get; set; }

    public int FilasProcesadas { get; set; }
    public int DiasActualizados { get; set; }
    public int AdicionalesActualizados { get; set; }

    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}
