namespace VIP_GATERING.WebUI.Controllers;

public class MenuClientesVM
{
    public string? Q { get; set; }
    public List<Item> Sucursales { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public class Item
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Rnc { get; set; }
        public int Sucursales { get; set; }
    }
}

public class MenuSucursalesVM
{
    public string? Q { get; set; }
    public Guid? EmpresaId { get; set; }
    public List<Grupo> Grupos { get; set; } = new();
    public class Grupo
    {
        public Guid EmpresaId { get; set; }
        public string Empresa { get; set; } = string.Empty;
        public List<SucItem> Sucursales { get; set; } = new();
    }
    public class SucItem
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }
}


