using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace VIP_GATERING.WebUI.Models;

public class ProductosImportVM
{
    public IFormFile? Archivo { get; set; }

    public int TotalFilas { get; set; }
    public int FilasProcesadas { get; set; }
    public int ProductosCreados { get; set; }
    public int ProductosActualizados { get; set; }
    public int ProductosSaltados { get; set; }

    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
}
