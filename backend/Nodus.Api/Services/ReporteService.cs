using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;

namespace Nodus.Api.Services;

public record ConsolidadoEquipo(string Codigo, string Familia, string Marca, string Modelo, string NroSerie,
    string Servicio, EstadoEquipo Estado, DateOnly Garantia, DateOnly Preventivo);
public record ConsolidadoOt(int Id, string Codigo, TipoMantenimiento Tipo, EstadoOt Estado, DateTime Apertura,
    DateTime? Cierre, string? Tecnico, ResultadoSeguridadElectrica SegElectrica, string? Tarea);
public record ConsolidadoMovimiento(DateTime Fecha, string Codigo, string Detalle, string Usuario);
public record ConsolidadoPrestamo(int Id, TipoDireccionPrestamo Tipo, EstadoPrestamo Estado, string Equipo, string Hospital,
    DateOnly Salida, DateOnly Estimada, DateOnly? Real);
public record ConsolidadoInsumo(string Insumo, int Consumido, int Repuesto, int StockActual, EstadoStock Estado);

public record ConsolidadoData(
    int Anio, int Mes, string Hospital, DateTime GeneradoLocal, string GeneradoPor,
    decimal DisponibilidadGlobal, decimal Umbral, List<DisponibilidadTipoDto> PorTipo,
    decimal? MttrHorasMes, int OtsAbiertasMes, int OtsCerradasMes, int ReportesMes, int ReportesDescartadosMes,
    List<ConsolidadoEquipo> Inventario,
    List<ConsolidadoOt> Ordenes,
    List<ConsolidadoMovimiento> Altas, List<ConsolidadoMovimiento> Bajas, List<ConsolidadoMovimiento> Traslados,
    List<ConsolidadoPrestamo> Prestamos,
    List<ConsolidadoInsumo> Insumos)
{
    public string Periodo => $"{Fechas.MesesEs[Mes]} {Anio}";
}

public class ReporteService(NodusDbContext db, ParametroService parametros)
{
    private static string Semaforo(decimal pct, decimal umbral) =>
        pct < umbral ? "CRITICO" : pct < umbral + 10 ? "ADVERTENCIA" : "OK";

    private async Task<(decimal global, List<DisponibilidadTipoDto> porTipo, int total)> DisponibilidadAsync(decimal umbral, CancellationToken ct)
    {
        var filas = await db.Equipos.AsNoTracking()
            .Where(e => e.EstadoOperativo != EstadoEquipo.BAJA_PATRIMONIAL)
            .GroupBy(e => new { e.IdFamilia, e.Familia.NombreFamilia })
            .Select(g => new
            {
                g.Key.IdFamilia, g.Key.NombreFamilia, Total = g.Count(),
                Operativos = g.Count(e => e.EstadoOperativo == EstadoEquipo.OPERATIVO),
            })
            .ToListAsync(ct);

        var porTipo = filas.OrderBy(f => f.NombreFamilia).Select(f =>
        {
            var pct = f.Total == 0 ? 0 : Math.Round(100m * f.Operativos / f.Total, 1);
            return new DisponibilidadTipoDto(f.IdFamilia, f.NombreFamilia, f.Total, f.Operativos, pct, Semaforo(pct, umbral));
        }).ToList();

        var total = filas.Sum(f => f.Total);
        var global = total == 0 ? 100m : Math.Round(100m * filas.Sum(f => f.Operativos) / total, 1);
        return (global, porTipo, total);
    }

    /// <summary>Tiempo medio de reparación en horas de las OT correctivas finalizadas.</summary>
    private async Task<(decimal? horas, int n)> MttrAsync(DateTime? cierreDesde, DateTime? cierreHasta, CancellationToken ct)
    {
        var q = db.OrdenesTrabajo.AsNoTracking()
            .Where(o => o.TipoMantenimiento == TipoMantenimiento.CORRECTIVO && o.EstadoOt == EstadoOt.FINALIZADA && o.FechaCierre != null);
        if (cierreDesde is DateTime d) q = q.Where(o => o.FechaCierre >= d);
        if (cierreHasta is DateTime h) q = q.Where(o => o.FechaCierre < h);
        var pares = await q.Select(o => new { o.FechaApertura, o.FechaCierre }).ToListAsync(ct);
        if (pares.Count == 0) return (null, 0);
        var horas = pares.Average(p => (p.FechaCierre!.Value - p.FechaApertura).TotalHours);
        return (Math.Round((decimal)horas, 1), pares.Count);
    }

    public async Task<ConsolidadoData> ConsolidadoAsync(int anio, int mes, string generadoPor, CancellationToken ct)
    {
        if (mes is < 1 or > 12 || anio is < 2000 or > 2100)
            throw new BusinessException("Período inválido.");
        var (desde, hasta) = Fechas.RangoMesUtc(anio, mes);
        var desdeD = new DateOnly(anio, mes, 1);
        var hastaD = desdeD.AddMonths(1);

        var umbral = (decimal)await parametros.GetIntAsync(Parametros.UmbralDisponibilidad, ct);
        var (global, porTipo, _) = await DisponibilidadAsync(umbral, ct);
        var (mttr, _) = await MttrAsync(desde, hasta, ct);

        var inventario = await db.Equipos.AsNoTracking().OrderBy(e => e.CodigoPatrimonial)
            .Select(e => new ConsolidadoEquipo(e.CodigoPatrimonial, e.Familia.NombreFamilia, e.Marca.NombreMarca, e.Modelo,
                e.NroSerie, e.ServicioActual.NombreServicio, e.EstadoOperativo, e.FechaGarantiaVencimiento, e.FechaMantenimientoPreventivo))
            .ToListAsync(ct);

        var ordenes = await db.OrdenesTrabajo.AsNoTracking()
            .Where(o => (o.FechaApertura >= desde && o.FechaApertura < hasta)
                        || (o.FechaCierre >= desde && o.FechaCierre < hasta)
                        || (o.FechaApertura < hasta && (o.FechaCierre == null || o.FechaCierre >= hasta)))
            .OrderBy(o => o.FechaApertura)
            .Select(o => new ConsolidadoOt(o.IdOt, o.Equipo.CodigoPatrimonial, o.TipoMantenimiento, o.EstadoOt, o.FechaApertura,
                o.FechaCierre, o.TecnicoAsignado != null ? o.TecnicoAsignado.NombreCompleto : null,
                o.ResultadoSeguridadElectrica, o.DescripcionTareaTecnica))
            .ToListAsync(ct);

        var altas = await db.Equipos.AsNoTracking().Where(e => e.CreatedAt >= desde && e.CreatedAt < hasta)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new ConsolidadoMovimiento(e.CreatedAt, e.CodigoPatrimonial,
                e.Familia.NombreFamilia + " " + e.Marca.NombreMarca + " " + e.Modelo + " · S/N " + e.NroSerie, ""))
            .ToListAsync(ct);

        var bajas = await db.BajasPatrimoniales.AsNoTracking().Where(b => b.FechaBaja >= desdeD && b.FechaBaja < hastaD)
            .OrderBy(b => b.FechaBaja)
            .Select(b => new { b.FechaBaja, b.Equipo.CodigoPatrimonial, b.MotivoBaja, b.Observaciones, b.UsuarioBioingeniero.NombreCompleto })
            .ToListAsync(ct);

        var traslados = await db.HistorialUbicaciones.AsNoTracking()
            .Where(h => h.FechaHoraCambio >= desde && h.FechaHoraCambio < hasta)
            .OrderBy(h => h.FechaHoraCambio)
            .Select(h => new ConsolidadoMovimiento(h.FechaHoraCambio, h.Equipo.CodigoPatrimonial,
                h.ServicioOrigen.NombreServicio + " → " + h.ServicioDestino.NombreServicio, h.Usuario.NombreCompleto))
            .ToListAsync(ct);

        var prestamos = await db.Prestamos.AsNoTracking()
            .Where(p => p.FechaSalida < hastaD && (p.FechaDevolucionReal == null || p.FechaDevolucionReal >= desdeD))
            .OrderBy(p => p.FechaSalida)
            .Select(p => new ConsolidadoPrestamo(p.IdPrestamo, p.TipoDireccion, p.EstadoPrestamo,
                p.Equipo != null ? p.Equipo.CodigoPatrimonial : p.MarcaExterna + " " + p.ModeloExterno,
                p.HospitalExterno.NombreHospital, p.FechaSalida, p.FechaDevolucionEstimada, p.FechaDevolucionReal))
            .ToListAsync(ct);

        var consumos = await db.UsosInsumo.AsNoTracking().Where(u => u.FechaUso >= desde && u.FechaUso < hasta)
            .GroupBy(u => u.IdInsumo).Select(g => new { g.Key, Total = g.Sum(x => x.CantidadConsumida) }).ToListAsync(ct);
        var repos = await db.Reposiciones.AsNoTracking().Where(r => r.FechaReposicion >= desde && r.FechaReposicion < hasta)
            .GroupBy(r => r.IdInsumo).Select(g => new { g.Key, Total = g.Sum(x => x.CantidadIngresada) }).ToListAsync(ct);
        var insumos = (await db.Insumos.AsNoTracking().OrderBy(i => i.NombreInsumo).ToListAsync(ct))
            .Select(i => new ConsolidadoInsumo(i.NombreInsumo,
                consumos.FirstOrDefault(c => c.Key == i.IdInsumo)?.Total ?? 0,
                repos.FirstOrDefault(r => r.Key == i.IdInsumo)?.Total ?? 0,
                i.StockActual, i.EstadoStock))
            .ToList();

        return new ConsolidadoData(
            anio, mes, await parametros.GetAsync(Parametros.CodigoHospital, ct), Fechas.AhoraLocal, generadoPor,
            global, umbral, porTipo, mttr,
            ordenes.Count(o => o.Apertura >= desde && o.Apertura < hasta),
            ordenes.Count(o => o.Cierre >= desde && o.Cierre < hasta),
            await db.ReportesFalla.CountAsync(r => r.FechaHoraReporte >= desde && r.FechaHoraReporte < hasta, ct),
            await db.ReportesFalla.CountAsync(r => r.FechaHoraReporte >= desde && r.FechaHoraReporte < hasta
                                                   && r.EstadoReporte == EstadoReporte.DESCARTADO, ct),
            inventario, ordenes, altas,
            bajas.Select(b => new ConsolidadoMovimiento(b.FechaBaja.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc),
                b.CodigoPatrimonial, b.MotivoBaja + (b.Observaciones != null ? " · " + b.Observaciones : ""), b.NombreCompleto)).ToList(),
            traslados, prestamos, insumos);
    }
}
