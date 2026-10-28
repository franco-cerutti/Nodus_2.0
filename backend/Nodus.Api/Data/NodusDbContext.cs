using Microsoft.EntityFrameworkCore;
using Nodus.Api.Domain;

namespace Nodus.Api.Data;

/// <summary>Mapeo database-first del esquema NODUS. El esquema se administra con los scripts de /database.</summary>
public class NodusDbContext(DbContextOptions<NodusDbContext> options) : DbContext(options)
{
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<TipoEquipo> TiposEquipo => Set<TipoEquipo>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<ContadorPatrimonial> ContadoresPatrimoniales => Set<ContadorPatrimonial>();
    public DbSet<HistorialUbicacion> HistorialUbicaciones => Set<HistorialUbicacion>();
    public DbSet<BajaPatrimonial> BajasPatrimoniales => Set<BajaPatrimonial>();
    public DbSet<ReporteFalla> ReportesFalla => Set<ReporteFalla>();
    public DbSet<OrdenTrabajo> OrdenesTrabajo => Set<OrdenTrabajo>();
    public DbSet<DetalleMantenimientoTercerizado> Tercerizados => Set<DetalleMantenimientoTercerizado>();
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<UsoInsumoOt> UsosInsumo => Set<UsoInsumoOt>();
    public DbSet<ReposicionInsumo> Reposiciones => Set<ReposicionInsumo>();
    public DbSet<SolicitudCompraInsumo> SolicitudesCompra => Set<SolicitudCompraInsumo>();
    public DbSet<HospitalExterno> HospitalesExternos => Set<HospitalExterno>();
    public DbSet<PrestamoEquipo> Prestamos => Set<PrestamoEquipo>();
    public DbSet<ParametroSistema> Parametros => Set<ParametroSistema>();
    public DbSet<Alerta> Alertas => Set<Alerta>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<EstadoEquipo>().HaveConversion<string>();
        b.Properties<EstadoReporte>().HaveConversion<string>();
        b.Properties<EstadoOt>().HaveConversion<string>();
        b.Properties<TipoMantenimiento>().HaveConversion<string>();
        b.Properties<ResultadoSeguridadElectrica>().HaveConversion<string>();
        b.Properties<EstadoStock>().HaveConversion<string>();
        b.Properties<EstadoPrestamo>().HaveConversion<string>();
        b.Properties<TipoDireccionPrestamo>().HaveConversion<string>();
        b.Properties<MotivoBaja>().HaveConversion<string>();
        b.Properties<TipoAlerta>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Usuario>(e =>
        {
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.IdRol);
            e.HasOne(x => x.Servicio).WithMany().HasForeignKey(x => x.IdServicio);
        });

        m.Entity<Equipo>(e =>
        {
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.Familia).WithMany().HasForeignKey(x => x.IdFamilia);
            e.HasOne(x => x.Marca).WithMany().HasForeignKey(x => x.IdMarca);
            e.HasOne(x => x.ServicioActual).WithMany().HasForeignKey(x => x.IdServicioActual);
        });

        m.Entity<ContadorPatrimonial>().Property(x => x.IdFamilia).ValueGeneratedNever();

        m.Entity<HistorialUbicacion>(e =>
        {
            e.Property(x => x.FechaHoraCambio).HasDefaultValueSql("now()");
            e.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.IdEquipo);
            e.HasOne(x => x.ServicioOrigen).WithMany().HasForeignKey(x => x.IdServicioOrigen);
            e.HasOne(x => x.ServicioDestino).WithMany().HasForeignKey(x => x.IdServicioDestino);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.IdUsuario);
        });

        m.Entity<BajaPatrimonial>(e =>
        {
            e.Property(x => x.FechaBaja).HasDefaultValueSql("CURRENT_DATE");
            e.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.IdEquipo);
            e.HasOne(x => x.UsuarioBioingeniero).WithMany().HasForeignKey(x => x.IdUsuarioBioingeniero);
        });

        m.Entity<ReporteFalla>(e =>
        {
            e.Property(x => x.FechaHoraReporte).HasDefaultValueSql("now()");
            e.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.IdEquipo);
            e.HasOne(x => x.UsuarioSesion).WithMany().HasForeignKey(x => x.IdUsuarioSesion);
        });

        m.Entity<OrdenTrabajo>(e =>
        {
            e.Property(x => x.FechaApertura).HasDefaultValueSql("now()");
            e.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.IdEquipo);
            e.HasOne(x => x.ReporteFalla).WithOne().HasForeignKey<OrdenTrabajo>(x => x.IdReporteFalla);
            e.HasOne(x => x.TecnicoAsignado).WithMany().HasForeignKey(x => x.IdTecnicoAsignado);
            e.HasOne(x => x.UsuarioCreador).WithMany().HasForeignKey(x => x.IdUsuarioCreador);
            e.HasOne(x => x.InsumoFaltante).WithMany().HasForeignKey(x => x.IdInsumoFaltante);
            e.HasOne(x => x.Tercerizado).WithOne(t => t.OrdenTrabajo)
                .HasForeignKey<DetalleMantenimientoTercerizado>(t => t.IdOt);
            e.HasMany(x => x.Consumos).WithOne(c => c.OrdenTrabajo).HasForeignKey(c => c.IdOt);
        });

        m.Entity<Insumo>(e =>
        {
            e.Property(x => x.EstadoStock).HasComputedColumnSql(
                "CASE WHEN stock_actual = 0 THEN 'AGOTADO' WHEN stock_actual < stock_minimo THEN 'STOCK_BAJO' ELSE 'STOCK_NORMAL' END",
                stored: true);
        });

        m.Entity<UsoInsumoOt>(e =>
        {
            e.Property(x => x.FechaUso).HasDefaultValueSql("now()");
            e.HasOne(x => x.Insumo).WithMany().HasForeignKey(x => x.IdInsumo);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.IdUsuario);
        });

        m.Entity<ReposicionInsumo>(e =>
        {
            e.Property(x => x.FechaReposicion).HasDefaultValueSql("now()");
            e.HasOne(x => x.Insumo).WithMany().HasForeignKey(x => x.IdInsumo);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.IdUsuario);
        });

        m.Entity<SolicitudCompraInsumo>(e =>
        {
            e.Property(x => x.FechaSolicitud).HasDefaultValueSql("now()");
            e.HasOne(x => x.Insumo).WithMany().HasForeignKey(x => x.IdInsumo);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.IdUsuario);
        });

        m.Entity<HospitalExterno>(e =>
        {
            e.Property(x => x.Latitud).HasPrecision(10, 8);
            e.Property(x => x.Longitud).HasPrecision(11, 8);
        });

        m.Entity<PrestamoEquipo>(e =>
        {
            e.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.IdEquipo);
            e.HasOne(x => x.HospitalExterno).WithMany().HasForeignKey(x => x.IdHospitalExterno);
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.IdUsuario);
        });

        m.Entity<ParametroSistema>().Property(x => x.Clave).ValueGeneratedNever();
        m.Entity<Alerta>().Property(x => x.FechaGeneracion).HasDefaultValueSql("now()");
    }
}
