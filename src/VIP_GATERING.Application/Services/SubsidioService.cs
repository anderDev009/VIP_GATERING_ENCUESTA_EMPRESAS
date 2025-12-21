using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Application.Services;

public record SubsidioContext(
    bool OpcionSubsidiada,
    bool EmpleadoSubsidiado,
    bool EmpresaSubsidia,
    SubsidioTipo EmpresaSubsidioTipo,
    decimal EmpresaSubsidioValor,
    bool? SucursalSubsidia,
    SubsidioTipo? SucursalSubsidioTipo,
    decimal? SucursalSubsidioValor,
    SubsidioTipo? EmpleadoSubsidioTipo,
    decimal? EmpleadoSubsidioValor);

public record SubsidioResultado(decimal PrecioEmpleado, decimal SubsidioAplicado);

public interface ISubsidioService
{
    SubsidioResultado CalcularPrecioEmpleado(decimal precioBase, SubsidioContext context);
}

public class SubsidioService : ISubsidioService
{
    public SubsidioResultado CalcularPrecioEmpleado(decimal precioBase, SubsidioContext context)
    {
        var basePrecio = precioBase < 0 ? 0 : precioBase;

        if (!context.OpcionSubsidiada || !context.EmpleadoSubsidiado)
            return new SubsidioResultado(basePrecio, 0m);

        var tieneOverrideEmpleado = context.EmpleadoSubsidioTipo != null || context.EmpleadoSubsidioValor != null;
        var subsidia = tieneOverrideEmpleado ? true : (context.SucursalSubsidia ?? context.EmpresaSubsidia);
        if (!subsidia)
            return new SubsidioResultado(basePrecio, 0m);

        var tipo = context.EmpleadoSubsidioTipo ?? context.SucursalSubsidioTipo ?? context.EmpresaSubsidioTipo;
        var valor = context.EmpleadoSubsidioValor ?? context.SucursalSubsidioValor ?? context.EmpresaSubsidioValor;
        if (valor <= 0)
            return new SubsidioResultado(basePrecio, 0m);

        decimal subsidioAplicado = tipo switch
        {
            SubsidioTipo.MontoFijo => valor,
            _ => Math.Round(basePrecio * (valor / 100m), 2)
        };

        if (subsidioAplicado > basePrecio) subsidioAplicado = basePrecio;

        var precioEmpleado = Math.Max(0, basePrecio - subsidioAplicado);
        return new SubsidioResultado(precioEmpleado, subsidioAplicado);
    }
}
