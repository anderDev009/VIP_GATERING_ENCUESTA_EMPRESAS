using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;

namespace VIP_GATERING.Infrastructure.Data;

public static class SeedData
{
    public static async Task EnsureSeedAsync(AppDbContext db)
    {
        // Horarios por defecto
        if (!await db.Horarios.AnyAsync())
        {
            await db.Horarios.AddRangeAsync(
                new Horario { Nombre = "Desayuno", Orden = 1, Activo = true },
                new Horario { Nombre = "Almuerzo", Orden = 2, Activo = true }
            );
            await db.SaveChangesAsync();
        }
        // Empresas
        var empresaDemo = await db.Empresas.FirstOrDefaultAsync(e => e.Nombre == "Empresa Demo");
        if (empresaDemo == null)
        {
            empresaDemo = new Empresa { Nombre = "Empresa Demo", Rnc = "RNC-000" };
            db.Empresas.Add(empresaDemo);
        }
        var empresaBeta = await db.Empresas.FirstOrDefaultAsync(e => e.Nombre == "Empresa Beta");
        if (empresaBeta == null)
        {
            empresaBeta = new Empresa { Nombre = "Empresa Beta", Rnc = "RNC-111" };
            db.Empresas.Add(empresaBeta);
        }
        await db.SaveChangesAsync();

        // Sucursales
        async Task<Sucursal> EnsureSucursal(string nombre, Guid empresaId)
        {
            var s = await db.Sucursales.FirstOrDefaultAsync(x => x.Nombre == nombre && x.EmpresaId == empresaId);
            if (s == null)
            {
                s = new Sucursal { Nombre = nombre, EmpresaId = empresaId };
                db.Sucursales.Add(s);
                await db.SaveChangesAsync();
            }
            return s;
        }

        var sucDemoPrincipal = await EnsureSucursal("Principal", empresaDemo.Id);
        var sucDemoNorte = await EnsureSucursal("Norte", empresaDemo.Id);
        var sucBetaEste = await EnsureSucursal("Este", empresaBeta.Id);
        var sucBetaOeste = await EnsureSucursal("Oeste", empresaBeta.Id);

        // Empleados
        async Task EnsureEmpleado(string nombre, Guid sucursalId)
        {
            if (!await db.Empleados.AnyAsync(e => e.Nombre == nombre && e.SucursalId == sucursalId))
            {
                db.Empleados.Add(new Empleado { Nombre = nombre, SucursalId = sucursalId });
                await db.SaveChangesAsync();
            }
        }
        await EnsureEmpleado("Juan Perez", sucDemoPrincipal.Id);
        await EnsureEmpleado("Ana Gomez", sucDemoPrincipal.Id);
        await EnsureEmpleado("Carlos Diaz", sucDemoNorte.Id);
        await EnsureEmpleado("Maria Lopez", sucBetaEste.Id);
        await EnsureEmpleado("Pedro Santos", sucBetaOeste.Id);

        // Opciones (si hay pocas, completar catálogo de ejemplo)
        if (await db.Opciones.CountAsync() < 10)
        {
            var opciones = new[]
            {
                new Opcion{ Nombre = "Pollo a la plancha", Descripcion = "Con ensalada", Costo = 5.5m },
                new Opcion{ Nombre = "Pasta bolognesa", Descripcion = "Con parmesano", Costo = 6.2m },
                new Opcion{ Nombre = "Ensalada mixta", Descripcion = "Veg", Costo = 4.0m },
                new Opcion{ Nombre = "Carne guisada", Descripcion = "Con arroz", Costo = 6.5m },
                new Opcion{ Nombre = "Pescado al horno", Descripcion = "Con verduras", Costo = 7.1m },
                new Opcion{ Nombre = "Lasaña de carne", Descripcion = "Con salsa bechamel", Costo = 6.9m },
                new Opcion{ Nombre = "Sopa de verduras", Descripcion = "Ligera", Costo = 3.2m },
                new Opcion{ Nombre = "Sandwich de pavo", Descripcion = "Con queso", Costo = 4.8m },
                new Opcion{ Nombre = "Arroz con pollo", Descripcion = "Clásico", Costo = 5.9m },
                new Opcion{ Nombre = "Tacos mixtos", Descripcion = "3 unidades", Costo = 6.1m }
            };
            // Evitar duplicados por nombre
            foreach (var op in opciones)
            {
                if (!await db.Opciones.AnyAsync(o => o.Nombre == op.Nombre))
                    await db.Opciones.AddAsync(op);
            }
            await db.SaveChangesAsync();
        }

        // Semilla legacy mínima de roles/usuarios de dominio (sin duplicar)
        if (!await db.Roles.AnyAsync(r => r.Nombre == "Admin"))
            db.Roles.Add(new Rol { Nombre = "Admin" });
        if (!await db.Roles.AnyAsync(r => r.Nombre == "Empleado"))
            db.Roles.Add(new Rol { Nombre = "Empleado" });
        await db.SaveChangesAsync();
    }
}
