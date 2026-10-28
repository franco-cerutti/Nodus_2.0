using Microsoft.EntityFrameworkCore;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;
using Npgsql;

namespace Nodus.Api.Services;

/// <summary>Reportes de falla, órdenes de trabajo, seguridad eléctrica y consumos de insumos.</summary>
public class MantenimientoService(NodusDbContext db, ICurrentUser user, ParametroService parametros)
{
    private IQueryable<ReporteFallaDto> ProyectarReportes(IQueryable<ReporteFalla> q) =>
        q.Select(r => new ReporteFallaDto(
            r.IdReporte, r.IdEquipo, r.Equipo.CodigoPatrimonial,
            r.Equipo.Familia.NombreFamilia + " " + r.Equipo.Marca.NombreMarca + " " + r.Equipo.Modelo,
            r.Equipo.ServicioActual.NombreServicio,
            r.NombreSolicitante + " " + r.ApellidoSolicitante, r.MatriculaSolicitante,
            r.DescripcionFalla, r.FechaHoraReporte, r.EstadoReporte, r.MotivoDescarte, r.UsuarioSesion.NombreCompleto));

    public Task<List<ReporteFallaDto>> ListarReportesAsync(EstadoReporte? estado, int? idEquipo, CancellationToken ct)
    {
        var q = db.ReportesFalla.AsNoTracking();
        if (estado is EstadoReporte e) q = q.Where(r => r.EstadoReporte == e);
        if (idEquipo is int id) q = q.Where(r => r.IdEquipo == id);
        return ProyectarReportes(q.OrderByDescending(r => r.FechaHoraReporte)).ToListAsync(ct);
    }

    /// <summary>Reportes pendientes posteriores a <paramref name="despuesDe"/> para la alarma visual.</summary>
    public async Task<NovedadesFallasDto> NovedadesAsync(int? despuesDe, CancellationToken ct)
    {
        var pendientes = db.ReportesFalla.AsNoTracking().Where(r => r.EstadoReporte == EstadoReporte.PENDIENTE);
        var total = await pendientes.CountAsync(ct);
        var ultimoId = await db.ReportesFalla.AsNoTracking().MaxAsync(r => (int?)r.IdReporte, ct) ?? 0;
        var nuevos = despuesDe is int id
            ? await ProyectarReportes(pendientes.Where(r => r.IdReporte > id).OrderByDescending(r => r.IdReporte)).Take(20).ToListAsync(ct)
            : new List<ReporteFallaDto>();
        return new NovedadesFallasDto(total, ultimoId, nuevos);
    }

    public async Task<ReporteFallaDto> ObtenerReporteAsync(int id, CancellationToken ct) =>
        await ProyectarReportes(db.ReportesFalla.AsNoTracking().Where(r => r.IdReporte == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"No existe el reporte {id}");

    /// <summary>Confirma el reporte y genera la OT correctiva.</summary>
    public async Task<OtDto> ConvertirReporteAsync(int idReporte, ConvertirReporteRequest r, CancellationToken ct)
    {
        var reporte = await db.ReportesFalla.AsNoTracking().FirstOrDefaultAsync(x => x.IdReporte == idReporte, ct)
                      ?? throw new NotFoundException($"No existe el reporte {idReporte}");
        if (reporte.EstadoReporte != EstadoReporte.PENDIENTE)
            throw new BusinessException($"El reporte ya está {reporte.EstadoReporte}.", StatusCodes.Status409Conflict);
        await ValidarTecnicoAsync(r.IdTecnicoAsignado, ct);

        var ot = new OrdenTrabajo
        {
            IdEquipo = reporte.IdEquipo,
            IdReporteFalla = reporte.IdReporte,
            TipoMantenimiento = TipoMantenimiento.CORRECTIVO,
            EstadoOt = EstadoOt.PENDIENTE,
            FechaApertura = DateTime.UtcNow,
            ResultadoSeguridadElectrica = ResultadoSeguridadElectrica.PENDIENTE,
            IdTecnicoAsignado = r.IdTecnicoAsignado,
            IdUsuarioCreador = user.Id,
        };
        db.OrdenesTrabajo.Add(ot);
        await db.SaveChangesAsync(ct);
        return await ObtenerOtAsync(ot.IdOt, ct);
    }

    public async Task<ReporteFallaDto> DescartarReporteAsync(int idReporte, DescartarReporteRequest r, CancellationToken ct)
    {
        var reporte = await db.ReportesFalla.FirstOrDefaultAsync(x => x.IdReporte == idReporte, ct)
                      ?? throw new NotFoundException($"No existe el reporte {idReporte}");
        if (reporte.EstadoReporte != EstadoReporte.PENDIENTE)
            throw new BusinessException($"El reporte ya está {reporte.EstadoReporte}.", StatusCodes.Status409Conflict);

        reporte.EstadoReporte = EstadoReporte.DESCARTADO;
        reporte.MotivoDescarte = r.Motivo.Trim();
        await db.SaveChangesAsync(ct);
        return await ObtenerReporteAsync(idReporte, ct);
    }

    private IQueryable<OtDto> ProyectarOts(IQueryable<OrdenTrabajo> q, DateOnly hoy) =>
        q.Select(o => new OtDto(
            o.IdOt, o.IdEquipo, o.Equipo.CodigoPatrimonial,
            o.Equipo.Familia.NombreFamilia + " " + o.Equipo.Marca.NombreMarca + " " + o.Equipo.Modelo,
            o.Equipo.ServicioActual.NombreServicio, o.Equipo.EstadoOperativo,
            o.IdReporteFalla, o.ReporteFalla != null ? o.ReporteFalla.DescripcionFalla : null,
            o.TipoMantenimiento, o.EstadoOt,
            o.FechaApertura, o.FechaInicioTaller, o.FechaCierre,
            o.DescripcionTareaTecnica, o.ResultadoSeguridadElectrica, o.ObservacionesSeguridadElectrica,
            o.IdTecnicoAsignado, o.TecnicoAsignado != null ? o.TecnicoAsignado.NombreCompleto : null,
            o.UsuarioCreador.NombreCompleto,
            o.Tercerizado == null ? null : new TercerizadoDto(
                o.Tercerizado.IdTercerizado, o.Tercerizado.ProveedorFabricaNombre, o.Tercerizado.EnGarantia,
                o.Tercerizado.FechaEnvio, o.Tercerizado.FechaProbableVuelta, o.Tercerizado.FechaVueltaReal,
                o.Tercerizado.RemitoEntradaDescripcion,
                o.Tercerizado.FechaVueltaReal == null && o.Tercerizado.FechaProbableVuelta < hoy),
            o.Consumos.OrderBy(c => c.FechaUso).Select(c => new ConsumoDto(
                c.IdUso, c.IdInsumo, c.Insumo.NombreInsumo, c.CantidadConsumida, c.FechaUso, c.Usuario.NombreCompleto)).ToList(),
            o.IdInsumoFaltante,
            o.InsumoFaltante != null ? o.InsumoFaltante.NombreInsumo : null,
            o.InsumoFaltante != null ? (int?)o.InsumoFaltante.StockActual : null));

    public Task<List<OtDto>> ListarOtsAsync(EstadoOt? estado, TipoMantenimiento? tipo, int? idEquipo, bool soloAbiertas, CancellationToken ct,
        DateTime? cierreDesde = null)
    {
        var q = db.OrdenesTrabajo.AsNoTracking();
        if (cierreDesde is DateTime cd) q = q.Where(o => o.FechaCierre >= cd);
        if (estado is EstadoOt e) q = q.Where(o => o.EstadoOt == e);
        else if (soloAbiertas) q = q.Where(o => o.EstadoOt != EstadoOt.FINALIZADA);
        if (tipo is TipoMantenimiento t) q = q.Where(o => o.TipoMantenimiento == t);
        if (idEquipo is int id) q = q.Where(o => o.IdEquipo == id);
        return ProyectarOts(q.OrderByDescending(o => o.FechaApertura), Fechas.Hoy).AsSplitQuery().ToListAsync(ct);
    }

    public async Task<OtDto> ObtenerOtAsync(int id, CancellationToken ct) =>
        await ProyectarOts(db.OrdenesTrabajo.AsNoTracking().Where(o => o.IdOt == id), Fechas.Hoy).AsSplitQuery().FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"No existe la OT {id}");

    /// <summary>Alta manual de OT (preventivo, correctivo sin reporte o tercerizado).</summary>
    public async Task<OtDto> CrearOtAsync(CrearOtRequest r, CancellationToken ct)
    {
        await ValidarTecnicoAsync(r.IdTecnicoAsignado, ct);
        if (!await db.Equipos.AnyAsync(e => e.IdEquipo == r.IdEquipo, ct))
            throw new NotFoundException($"No existe el equipo {r.IdEquipo}");

        var ahora = DateTime.UtcNow;
        var ot = new OrdenTrabajo
        {
            IdEquipo = r.IdEquipo,
            TipoMantenimiento = r.Tipo,
            EstadoOt = EstadoOt.PENDIENTE,
            FechaApertura = ahora,
            DescripcionTareaTecnica = r.DescripcionTareaTecnica?.Trim(),
            ResultadoSeguridadElectrica = ResultadoSeguridadElectrica.PENDIENTE,
            IdTecnicoAsignado = r.IdTecnicoAsignado,
            IdUsuarioCreador = user.Id,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (r.Tipo == TipoMantenimiento.TERCERIZADO)
        {
            if (r.Tercerizado is null)
                throw new BusinessException("Una OT tercerizada requiere los datos del proveedor y la fecha de envío.");
            ot.EstadoOt = EstadoOt.EN_CURSO;
            ot.FechaInicioTaller = ahora;
            ot.Tercerizado = await NuevoTercerizadoAsync(r.Tercerizado, ct);
        }

        db.OrdenesTrabajo.Add(ot);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ObtenerOtAsync(ot.IdOt, ct);
    }

    /// <summary>Deriva una OT abierta a reparación externa.</summary>
    public async Task<OtDto> TercerizarAsync(int idOt, TercerizadoRequest r, CancellationToken ct)
    {
        var ot = await CargarOtAbiertaAsync(idOt, ct);
        if (await db.Tercerizados.AnyAsync(t => t.IdOt == idOt, ct))
            throw new BusinessException("La OT ya está tercerizada.", StatusCodes.Status409Conflict);

        ot.TipoMantenimiento = TipoMantenimiento.TERCERIZADO;
        if (ot.EstadoOt == EstadoOt.PENDIENTE)
        {
            ot.EstadoOt = EstadoOt.EN_CURSO;
            ot.FechaInicioTaller = DateTime.UtcNow;
        }
        ot.Tercerizado = await NuevoTercerizadoAsync(r, ct);
        await db.SaveChangesAsync(ct);
        return await ObtenerOtAsync(idOt, ct);
    }

    private async Task<DetalleMantenimientoTercerizado> NuevoTercerizadoAsync(TercerizadoRequest r, CancellationToken ct)
    {
        var dias = await parametros.GetIntAsync(Parametros.DiasRetornoTercerizado, ct);
        return new DetalleMantenimientoTercerizado
        {
            ProveedorFabricaNombre = r.ProveedorFabricaNombre.Trim(),
            EnGarantia = r.EnGarantia,
            FechaEnvio = r.FechaEnvio,
            FechaProbableVuelta = r.FechaEnvio.AddDays(dias),
        };
    }

    public async Task<OtDto> RegistrarRetornoAsync(int idOt, RetornoTercerizadoRequest r, CancellationToken ct)
    {
        await CargarOtAbiertaAsync(idOt, ct);
        var t = await db.Tercerizados.FirstOrDefaultAsync(x => x.IdOt == idOt, ct)
                ?? throw new BusinessException("La OT no es tercerizada.");
        if (r.FechaVueltaReal > Fechas.Hoy)
            throw new BusinessException("La fecha de vuelta no puede ser futura.");

        t.FechaVueltaReal = r.FechaVueltaReal;
        t.RemitoEntradaDescripcion = r.RemitoEntradaDescripcion?.Trim();
        await db.SaveChangesAsync(ct);

        await db.Alertas
            .Where(a => a.TipoAlerta == TipoAlerta.TERCERIZADO_VENCIDO && a.IdReferencia == t.IdTercerizado && !a.Resuelta)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Resuelta, true), ct);

        return await ObtenerOtAsync(idOt, ct);
    }

    public async Task<OtDto> ActualizarOtAsync(int idOt, ActualizarOtRequest r, CancellationToken ct)
    {
        var ot = await CargarOtAbiertaAsync(idOt, ct);
        await ValidarTecnicoAsync(r.IdTecnicoAsignado, ct);
        ot.DescripcionTareaTecnica = r.DescripcionTareaTecnica?.Trim();
        ot.IdTecnicoAsignado = r.IdTecnicoAsignado;
        await db.SaveChangesAsync(ct);
        return await ObtenerOtAsync(idOt, ct);
    }

    /// <summary>Ingresa la OT al taller (EN_CURSO).</summary>
    public async Task<OtDto> IniciarOtAsync(int idOt, IniciarOtRequest r, CancellationToken ct)
    {
        var ot = await CargarOtAbiertaAsync(idOt, ct);
        if (ot.EstadoOt != EstadoOt.PENDIENTE)
            throw new BusinessException("La OT ya está en curso.", StatusCodes.Status409Conflict);
        if (r.IdTecnicoAsignado is int t)
        {
            await ValidarTecnicoAsync(t, ct);
            ot.IdTecnicoAsignado = t;
        }
        ot.IdTecnicoAsignado ??= user.Id;
        ot.EstadoOt = EstadoOt.EN_CURSO;
        ot.FechaInicioTaller = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ObtenerOtAsync(idOt, ct);
    }

    public async Task<OtDto> RegistrarSeguridadElectricaAsync(int idOt, SeguridadElectricaRequest r, CancellationToken ct)
    {
        var ot = await CargarOtAbiertaAsync(idOt, ct);
        if (ot.EstadoOt != EstadoOt.EN_CURSO)
            throw new BusinessException("La prueba de seguridad eléctrica se registra con la OT en curso.");
        if (r.Resultado == ResultadoSeguridadElectrica.NO_APROBADA && string.IsNullOrWhiteSpace(r.Observaciones))
            throw new BusinessException("Si la prueba no fue aprobada, detallá las observaciones.");

        ot.ResultadoSeguridadElectrica = r.Resultado;
        ot.ObservacionesSeguridadElectrica = r.Observaciones?.Trim();
        await db.SaveChangesAsync(ct);
        return await ObtenerOtAsync(idOt, ct);
    }

    /// <summary>Finaliza la OT. Requiere la prueba de seguridad eléctrica aprobada.</summary>
    public async Task<OtDto> FinalizarOtAsync(int idOt, FinalizarOtRequest r, CancellationToken ct)
    {
        var ot = await CargarOtAbiertaAsync(idOt, ct);
        if (ot.EstadoOt != EstadoOt.EN_CURSO)
            throw new BusinessException("Solo se puede finalizar una OT que esté en curso en el taller.");

        if (!string.IsNullOrWhiteSpace(r.DescripcionTareaTecnica))
            ot.DescripcionTareaTecnica = r.DescripcionTareaTecnica.Trim();
        if (string.IsNullOrWhiteSpace(ot.DescripcionTareaTecnica))
            throw new BusinessException("Describí la tarea técnica realizada antes de finalizar la OT.");

        if (ot.TipoMantenimiento == TipoMantenimiento.TERCERIZADO
            && await db.Tercerizados.AnyAsync(t => t.IdOt == idOt && t.FechaVueltaReal == null, ct))
            throw new BusinessException("Registrá primero el retorno del equipo desde el proveedor externo.");

        ot.EstadoOt = EstadoOt.FINALIZADA;
        ot.FechaCierre = DateTime.UtcNow;
        ot.IdInsumoFaltante = null;
        await db.SaveChangesAsync(ct);
        return await ObtenerOtAsync(idOt, ct);
    }

    public async Task<OtDto> AgregarConsumoAsync(int idOt, CrearConsumoRequest r, CancellationToken ct)
    {
        if (!await db.OrdenesTrabajo.AnyAsync(o => o.IdOt == idOt, ct))
            throw new NotFoundException($"No existe la OT {idOt}");

        db.UsosInsumo.Add(new UsoInsumoOt
        {
            IdOt = idOt,
            IdInsumo = r.IdInsumo,
            CantidadConsumida = r.Cantidad,
            FechaUso = DateTime.UtcNow,
            IdUsuario = user.Id,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "NS007" })
        {
            db.ChangeTracker.Clear();
            await db.OrdenesTrabajo
                .Where(o => o.IdOt == idOt && o.EstadoOt != EstadoOt.FINALIZADA)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.IdInsumoFaltante, (int?)r.IdInsumo), ct);
            throw;
        }

        await db.OrdenesTrabajo
            .Where(o => o.IdOt == idOt && o.IdInsumoFaltante == r.IdInsumo)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IdInsumoFaltante, (int?)null), ct);
        return await ObtenerOtAsync(idOt, ct);
    }

    /// <summary>Marca o quita manualmente el bloqueo de una OT por falta de insumo.</summary>
    public async Task<OtDto> MarcarEsperaInsumoAsync(int idOt, int? idInsumo, CancellationToken ct)
    {
        var ot = await CargarOtAbiertaAsync(idOt, ct);
        if (idInsumo is int i && !await db.Insumos.AnyAsync(x => x.IdInsumo == i, ct))
            throw new BusinessException("El insumo indicado no existe.");
        ot.IdInsumoFaltante = idInsumo;
        await db.SaveChangesAsync(ct);
        return await ObtenerOtAsync(idOt, ct);
    }

    public async Task<OtDto> EliminarConsumoAsync(int idOt, int idUso, CancellationToken ct)
    {
        var uso = await db.UsosInsumo.FirstOrDefaultAsync(u => u.IdUso == idUso && u.IdOt == idOt, ct)
                  ?? throw new NotFoundException($"No existe el consumo {idUso} en la OT {idOt}");
        db.UsosInsumo.Remove(uso);
        await db.SaveChangesAsync(ct);
        return await ObtenerOtAsync(idOt, ct);
    }

    private async Task<OrdenTrabajo> CargarOtAbiertaAsync(int idOt, CancellationToken ct)
    {
        var ot = await db.OrdenesTrabajo.FirstOrDefaultAsync(o => o.IdOt == idOt, ct)
                 ?? throw new NotFoundException($"No existe la OT {idOt}");
        if (ot.EstadoOt == EstadoOt.FINALIZADA)
            throw new BusinessException($"La OT {idOt} ya está finalizada y no admite cambios.", StatusCodes.Status409Conflict);
        return ot;
    }

    private async Task ValidarTecnicoAsync(int? idTecnico, CancellationToken ct)
    {
        if (idTecnico is not int id) return;
        var ok = await db.Usuarios.AnyAsync(u => u.IdUsuario == id && u.Activo
            && (u.Rol.NombreRol == Roles.Tecnico || u.Rol.NombreRol == Roles.Bioingeniero), ct);
        if (!ok) throw new BusinessException("El técnico asignado no existe, está inactivo o no pertenece al sector.");
    }
}
