using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VIP_GATERING.Domain.Entities;
using VIP_GATERING.Infrastructure.Identity;

namespace VIP_GATERING.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public new DbSet<Rol> Roles => Set<Rol>();
    public DbSet<RolUsuario> RolesUsuario => Set<RolUsuario>();
    public DbSet<Opcion> Opciones => Set<Opcion>();
    public DbSet<OpcionHorario> OpcionesHorarios => Set<OpcionHorario>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<OpcionMenu> OpcionesMenu => Set<OpcionMenu>();
    public DbSet<Horario> Horarios => Set<Horario>();
    public DbSet<SucursalHorario> SucursalesHorarios => Set<SucursalHorario>();
    public DbSet<RespuestaFormulario> RespuestasFormulario => Set<RespuestaFormulario>();
    public DbSet<MenuConfiguracion> ConfiguracionesMenu => Set<MenuConfiguracion>();
    public DbSet<EmpleadoSucursal> EmpleadosSucursales => Set<EmpleadoSucursal>();
    public DbSet<MenuAdicional> MenusAdicionales => Set<MenuAdicional>();
    public DbSet<Localizacion> Localizaciones => Set<Localizacion>();
    public DbSet<EmpleadoLocalizacion> EmpleadosLocalizaciones => Set<EmpleadoLocalizacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Empresa>()
            .Property(e => e.SubsidiaEmpleados)
            .HasDefaultValue(true);
        modelBuilder.Entity<Empresa>()
            .Property(e => e.SubsidioTipo)
            .HasDefaultValue(SubsidioTipo.Porcentaje);
        modelBuilder.Entity<Empresa>()
            .Property(e => e.SubsidioValor)
            .HasDefaultValue(75m);

        modelBuilder.Entity<Empleado>()
            .Property(e => e.EsSubsidiado)
            .HasDefaultValue(true);

        modelBuilder.Entity<Sucursal>()
            .HasOne(s => s.Empresa)
            .WithMany(e => e.Sucursales)
            .HasForeignKey(s => s.EmpresaId);

        modelBuilder.Entity<Empleado>()
            .HasOne(e => e.Sucursal)
            .WithMany(s => s.Empleados)
            .HasForeignKey(e => e.SucursalId);

        modelBuilder.Entity<EmpleadoSucursal>()
            .HasIndex(es => new { es.EmpleadoId, es.SucursalId })
            .IsUnique();
        modelBuilder.Entity<EmpleadoSucursal>()
            .HasOne(es => es.Empleado)
            .WithMany(e => e.SucursalesAsignadas)
            .HasForeignKey(es => es.EmpleadoId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EmpleadoSucursal>()
            .HasOne(es => es.Sucursal)
            .WithMany(s => s.EmpleadosAsignados)
            .HasForeignKey(es => es.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Localizacion>()
            .HasIndex(l => new { l.SucursalId, l.Nombre })
            .IsUnique();
        modelBuilder.Entity<Localizacion>()
            .HasOne(l => l.Sucursal)
            .WithMany(s => s.Localizaciones)
            .HasForeignKey(l => l.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmpleadoLocalizacion>()
            .HasIndex(el => new { el.EmpleadoId, el.LocalizacionId })
            .IsUnique();
        modelBuilder.Entity<EmpleadoLocalizacion>()
            .HasOne(el => el.Empleado)
            .WithMany(e => e.LocalizacionesAsignadas)
            .HasForeignKey(el => el.EmpleadoId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EmpleadoLocalizacion>()
            .HasOne(el => el.Localizacion)
            .WithMany(l => l.EmpleadosAsignados)
            .HasForeignKey(el => el.LocalizacionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Legacy Usuario entity mapping (kept for backward compatibility in the domain layer)
        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Empleado)
            .WithOne(e => e.Usuario)
            .HasForeignKey<Usuario>(u => u.EmpleadoId);

        // Identity user links to domain entities for scoping
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.EmpleadoId);
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.EmpresaId);

        modelBuilder.Entity<RolUsuario>()
            .HasOne(ru => ru.Rol)
            .WithMany(r => r.RolesUsuario)
            .HasForeignKey(ru => ru.RolId);

        modelBuilder.Entity<RolUsuario>()
            .HasOne(ru => ru.Usuario)
            .WithMany(u => u.RolesUsuario)
            .HasForeignKey(ru => ru.UsuarioId);

        modelBuilder.Entity<OpcionMenu>()
            .HasOne(om => om.Menu)
            .WithMany(m => m.OpcionesPorDia)
            .HasForeignKey(om => om.MenuId);

        modelBuilder.Entity<Menu>()
            .HasOne(m => m.Empresa)
            .WithMany()
            .HasForeignKey(m => m.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Menu>()
            .HasOne(m => m.Sucursal)
            .WithMany()
            .HasForeignKey(m => m.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OpcionMenu>()
            .HasOne(om => om.OpcionA)
            .WithMany()
            .HasForeignKey(om => om.OpcionIdA)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OpcionMenu>()
            .HasOne(om => om.OpcionB)
            .WithMany()
            .HasForeignKey(om => om.OpcionIdB)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OpcionMenu>()
            .HasOne(om => om.OpcionC)
            .WithMany()
            .HasForeignKey(om => om.OpcionIdC)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OpcionMenu>()
            .HasOne(om => om.OpcionD)
            .WithMany()
            .HasForeignKey(om => om.OpcionIdD)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OpcionMenu>()
            .HasOne(om => om.OpcionE)
            .WithMany()
            .HasForeignKey(om => om.OpcionIdE)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OpcionMenu>()
            .HasOne(om => om.Horario)
            .WithMany()
            .HasForeignKey(om => om.HorarioId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RespuestaFormulario>()
            .HasOne(r => r.OpcionMenu)
            .WithMany(om => om.Respuestas)
            .HasForeignKey(r => r.OpcionMenuId);

        modelBuilder.Entity<RespuestaFormulario>()
            .HasOne(r => r.SucursalEntrega)
            .WithMany()
            .HasForeignKey(r => r.SucursalEntregaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RespuestaFormulario>()
            .HasOne(r => r.LocalizacionEntrega)
            .WithMany()
            .HasForeignKey(r => r.LocalizacionEntregaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RespuestaFormulario>()
            .HasOne(r => r.AdicionalOpcion)
            .WithMany()
            .HasForeignKey(r => r.AdicionalOpcionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MenuAdicional>()
            .HasIndex(ma => new { ma.MenuId, ma.OpcionId })
            .IsUnique();
        modelBuilder.Entity<MenuAdicional>()
            .HasOne(ma => ma.Menu)
            .WithMany(m => m.Adicionales)
            .HasForeignKey(ma => ma.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<MenuAdicional>()
            .HasOne(ma => ma.Opcion)
            .WithMany()
            .HasForeignKey(ma => ma.OpcionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SucursalHorario>()
            .HasIndex(sh => new { sh.SucursalId, sh.HorarioId })
            .IsUnique();
        modelBuilder.Entity<SucursalHorario>()
            .HasOne(sh => sh.Sucursal)
            .WithMany()
            .HasForeignKey(sh => sh.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SucursalHorario>()
            .HasOne(sh => sh.Horario)
            .WithMany()
            .HasForeignKey(sh => sh.HorarioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OpcionHorario>()
            .HasIndex(oh => new { oh.OpcionId, oh.HorarioId })
            .IsUnique();
        modelBuilder.Entity<OpcionHorario>()
            .HasOne(oh => oh.Opcion)
            .WithMany(o => o.Horarios)
            .HasForeignKey(oh => oh.OpcionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<OpcionHorario>()
            .HasOne(oh => oh.Horario)
            .WithMany(h => h.Opciones)
            .HasForeignKey(oh => oh.HorarioId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MenuConfiguracion>()
            .Property(c => c.PermitirEdicionSemanaActual)
            .HasDefaultValue(true);
        modelBuilder.Entity<MenuConfiguracion>()
            .Property(c => c.DiasAnticipoSemanaActual)
            .HasDefaultValue(1);
        modelBuilder.Entity<MenuConfiguracion>()
            .Property(c => c.HoraLimiteEdicion)
            .HasConversion<long>()
            .HasDefaultValue(new TimeSpan(12, 0, 0));
        modelBuilder.Entity<MenuConfiguracion>()
            .Property(c => c.CreadoUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<MenuConfiguracion>()
            .Property(c => c.ActualizadoUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
