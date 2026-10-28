using Microsoft.EntityFrameworkCore;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;

namespace Nodus.Api.Services;

public class EquipoService(NodusDbContext db, ICurrentUser user, ParametroService parametros)
{
    internal async Task<IQueryable<EquipoListDto>> ProyectarAsync(IQueryable<Equipo> q, CancellationToken ct)
    {
        var hoy = Fechas.Hoy;
        var limite = hoy.AddDays(await parametros.GetIntAsync(Parametros.DiasAlertaPreventivo, ct));
        return q.Select(e => new EquipoListDto(
            e.IdEquipo, e.CodigoPatrimonial, e.IdFamilia, e.Familia.NombreFamilia, e.Marca.NombreMarca, e.Modelo, e.NroSerie,
            e.IdServicioActual, e.ServicioActual.NombreServicio, e.EstadoOperativo,
            e.FechaAdquisicion, e.FechaGarantiaVencimiento, e.FechaMantenimientoPreventivo,
            e.FechaGarantiaVencimiento >= hoy,
            e.EstadoOperativo != EstadoEquipo.BAJA_PATRIMONIAL && e.FechaMantenimientoPreventivo <= limite,
            e.Version));
    }

    public async Task<List<EquipoListDto>> ListarAsync(int? servicio, EstadoEquipo? estado, int? familia, string? q,
        bool incluirBajas, CancellationToken ct)
    {
        var query = db.Equipos.AsNoTracking();
        if (servicio is int s) query = query.Where(e => e.IdServicioActual == s);
        if (estado is EstadoEquipo est) query = query.Where(e => e.EstadoOperativo == est);
        else if (!incluirBajas) query = query.Where(e => e.EstadoOperativo != EstadoEquipo.BAJA_PATRIMONIAL);
        if (familia is int f) query = query.Where(e => e.IdFamilia == f);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.CodigoPatrimonial, like)
                                     || EF.Functions.ILike(e.NroSerie, like)
                                     || EF.Functions.ILike(e.Modelo, like));
        }
        return await (await ProyectarAsync(query.OrderBy(e => e.CodigoPatrimonial), ct)).ToListAsync(ct);
    }

    public async Task<EquipoListDto> ObtenerAsync(int id, CancellationToken ct) =>
        await (await ProyectarAsync(db.Equipos.AsNoTracking().Where(e => e.IdEquipo == id), ct)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"No existe el equipo {id}");

    public async Task<EquipoDetalleDto> DetalleAsync(int id, PrestamoService prestamos, MantenimientoService mant, CancellationToken ct)
    {
        var equipo = await ObtenerAsync(id, ct);

        var historial = await db.HistorialUbicaciones.AsNoTracking()
            .Where(h => h.IdEquipo == id)
            .OrderByDescending(h => h.FechaHoraCambio)
            .Select(h => new HistorialUbicacionDto(h.FechaHoraCambio, h.ServicioOrigen.NombreServicio,
                h.ServicioDestino.NombreServicio, h.Usuario.NombreCompleto))
            .ToListAsync(ct);

        var ordenes = await db.OrdenesTrabajo.AsNoTracking()
            .Where(o => o.IdEquipo == id)
            .OrderByDescending(o => o.FechaApertura)
            .Select(o => new OtResumenDto(o.IdOt, o.TipoMantenimiento, o.EstadoOt, o.FechaApertura, o.FechaCierre,
                o.DescripcionTareaTecnica))
            .ToListAsync(ct);

        var baja = await db.BajasPatrimoniales.AsNoTracking()
            .Where(b => b.IdEquipo == id)
            .Select(b => new BajaDto(b.FechaBaja, b.MotivoBaja, b.Observaciones, b.UsuarioBioingeniero.NombreCompleto))
            .FirstOrDefaultAsync(ct);

        var reporte = (await mant.ListarReportesAsync(EstadoReporte.PENDIENTE, id, ct)).FirstOrDefault();

        return new EquipoDetalleDto(equipo, historial, ordenes, await prestamos.ListarAsync(null, null, id, ct), reporte, baja);
    }

    public async Task<KanbanDto> KanbanAsync(CancellationToken ct)
    {
        var servicios = await db.Servicios.AsNoTracking().OrderBy(s => s.IdServicio)
            .Select(s => new ItemDto(s.IdServicio, s.NombreServicio, s.CodigoServicio)).ToListAsync(ct);
        var equipos = await ListarAsync(null, null, null, null, incluirBajas: false, ct);
        return new KanbanDto(servicios, equipos);
    }

    public async Task<EquipoListDto> CrearAsync(CrearEquipoRequest r, CancellationToken ct)
    {
        if (r.FechaAdquisicion > Fechas.Hoy)
            throw new BusinessException("La fecha de adquisición no puede ser futura.");
        if (!await db.Marcas.AnyAsync(m => m.IdMarca == r.IdMarca, ct))
            throw new BusinessException("La marca indicada no existe.");

        var servicio = await db.Servicios.AsNoTracking().FirstOrDefaultAsync(s => s.IdServicio == r.IdServicio, ct)
                       ?? throw new BusinessException("El servicio indicado no existe.");
        if (!SiglasServicio.PorNombre.TryGetValue(servicio.NombreServicio, out var sigla))
            throw new BusinessException($"El servicio '{servicio.NombreServicio}' no tiene sigla patrimonial asignada.");
        if (servicio.CodigoServicio != sigla)
            throw new BusinessException($"La sigla registrada para '{servicio.NombreServicio}' ({servicio.CodigoServicio}) no coincide con la oficial ({sigla}). Ejecutá la migración 003.");

        var garantia = r.FechaAdquisicion.AddYears(1);
        var preventivo = garantia.AddDays(365);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var codigo = await db.Database
            .SqlQuery<string>($"SELECT fn_generar_codigo_patrimonial({r.IdServicio}, {r.IdFamilia}) AS \"Value\"")
            .SingleAsync(ct);

        var equipo = new Equipo
        {
            CodigoPatrimonial = codigo,
            IdFamilia = r.IdFamilia,
            IdMarca = r.IdMarca,
            Modelo = r.Modelo.Trim(),
            NroSerie = r.NroSerie.Trim(),
            FechaAdquisicion = r.FechaAdquisicion,
            FechaGarantiaVencimiento = garantia,
            FechaMantenimientoPreventivo = preventivo,
            IdServicioActual = r.IdServicio,
            EstadoOperativo = EstadoEquipo.OPERATIVO,
        };
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ObtenerAsync(equipo.IdEquipo, ct);
    }

    public async Task<EquipoListDto> TrasladarAsync(int id, TrasladoRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var equipo = await db.Equipos.FirstOrDefaultAsync(e => e.IdEquipo == id, ct)
                     ?? throw new NotFoundException($"No existe el equipo {id}");

        if (!await db.Servicios.AnyAsync(s => s.IdServicio == r.IdServicioDestino, ct))
            throw new BusinessException("El servicio de destino no existe.");
        if (equipo.IdServicioActual == r.IdServicioDestino)
            throw new BusinessException("El equipo ya se encuentra en ese servicio.");

        db.Entry(equipo).Property(e => e.Version).OriginalValue = r.Version;

        var origen = equipo.IdServicioActual;
        equipo.IdServicioActual = r.IdServicioDestino;

        db.HistorialUbicaciones.Add(new HistorialUbicacion
        {
            IdEquipo = id,
            IdServicioOrigen = origen,
            IdServicioDestino = r.IdServicioDestino,
            FechaHoraCambio = DateTime.UtcNow,
            IdUsuario = user.Id,
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ObtenerAsync(id, ct);
    }

    public async Task<EquipoListDto> DarDeBajaAsync(int id, BajaRequest r, CancellationToken ct)
    {
        var equipo = await db.Equipos.AsNoTracking().FirstOrDefaultAsync(e => e.IdEquipo == id, ct)
                     ?? throw new NotFoundException($"No existe el equipo {id}");

        if (!string.Equals(r.ConfirmacionCodigo?.Trim(), equipo.CodigoPatrimonial, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("La confirmación no coincide con el código patrimonial del equipo.");
        if (r.Motivo == MotivoBaja.COSTO_REPARACION_MAYOR_60 && string.IsNullOrWhiteSpace(r.Observaciones))
            throw new BusinessException("Para el motivo 'Costo de reparación > 60%' documentá el presupuesto y el valor del equipo en las observaciones.");

        db.BajasPatrimoniales.Add(new BajaPatrimonial
        {
            IdEquipo = id,
            FechaBaja = Fechas.Hoy,
            MotivoBaja = r.Motivo,
            Observaciones = r.Observaciones?.Trim(),
            IdUsuarioBioingeniero = user.Id,
        });
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<QrEquipoDto> QrAsync(string codigo, CancellationToken ct)
    {
        var codigoNorm = codigo.Trim().ToUpperInvariant();
        var e = await db.Equipos.AsNoTracking()
                    .Where(x => x.CodigoPatrimonial == codigoNorm)
                    .Select(x => new
                    {
                        x.IdEquipo, x.CodigoPatrimonial, Familia = x.Familia.NombreFamilia, Marca = x.Marca.NombreMarca,
                        x.Modelo, Servicio = x.ServicioActual.NombreServicio, x.EstadoOperativo,
                    })
                    .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException($"No se encontró el equipo {codigoNorm}");

        var reportePendiente = await db.ReportesFalla.AnyAsync(r => r.IdEquipo == e.IdEquipo && r.EstadoReporte == EstadoReporte.PENDIENTE, ct);
        var otAbierta = await db.OrdenesTrabajo.AnyAsync(o => o.IdEquipo == e.IdEquipo && o.EstadoOt != EstadoOt.FINALIZADA, ct);

        string? motivo = e.EstadoOperativo switch
        {
            EstadoEquipo.BAJA_PATRIMONIAL => "El equipo está dado de baja patrimonial.",
            EstadoEquipo.EN_PRESTAMO => "El equipo está prestado a otra institución.",
            _ when reportePendiente => "Ya hay una falla reportada para este equipo; Bioingeniería está al tanto.",
            _ when otAbierta => "El equipo ya está siendo atendido por Bioingeniería (OT abierta).",
            _ => null,
        };

        return new QrEquipoDto(e.IdEquipo, e.CodigoPatrimonial, e.Familia, e.Marca, e.Modelo, e.Servicio,
            e.EstadoOperativo, reportePendiente, motivo == null, motivo);
    }

    public async Task<ReporteFallaDto> ReportarFallaAsync(string codigo, CrearReporteRequest r, MantenimientoService mant, CancellationToken ct)
    {
        var codigoNorm = codigo.Trim().ToUpperInvariant();
        var idEquipo = await db.Equipos.Where(e => e.CodigoPatrimonial == codigoNorm).Select(e => (int?)e.IdEquipo).FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException($"No se encontró el equipo {codigoNorm}");

        var reporte = new ReporteFalla
        {
            IdEquipo = idEquipo,
            IdUsuarioSesion = user.Id,
            NombreSolicitante = r.Nombre.Trim(),
            ApellidoSolicitante = r.Apellido.Trim(),
            MatriculaSolicitante = r.Matricula.Trim(),
            DescripcionFalla = r.DescripcionFalla.Trim(),
            FechaHoraReporte = DateTime.UtcNow,
            EstadoReporte = EstadoReporte.PENDIENTE,
        };
        db.ReportesFalla.Add(reporte);
        await db.SaveChangesAsync(ct);
        return await mant.ObtenerReporteAsync(reporte.IdReporte, ct);
    }
}
