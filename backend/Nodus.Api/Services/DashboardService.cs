using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;

namespace Nodus.Api.Services;

/// <summary>
/// Indicadores del tablero de control. Las métricas de estado son en tiempo real; las de período usan el rango
/// desde/hasta. Una falla es una OT correctiva fechada por su reporte de falla o, en su defecto, por su apertura.
/// </summary>
public class DashboardService(NodusDbContext db, ParametroService parametros)
{
    private sealed record EqRow(int Id, string Codigo, int IdFamilia, string Familia, string CodigoFamilia, string Marca,
        string Modelo, int IdServicio, string Servicio, EstadoEquipo Estado, DateOnly Adquisicion, DateOnly Preventivo);

    private sealed record OtRow(int Id, int IdEquipo, TipoMantenimiento Tipo, EstadoOt Estado, DateTime Apertura,
        DateTime? Cierre, ResultadoSeguridadElectrica Seguridad, string? ObsSeguridad, int? IdInsumoFaltante, DateTime? FechaReporte)
    {
        /// <summary>Fecha y hora de la falla.</summary>
        public DateTime Momento => FechaReporte ?? Apertura;
    }

    public async Task<string> DescribirFiltrosAsync(DashboardDto d, CancellationToken ct)
    {
        var servicio = d.IdServicio is int s
            ? await db.Servicios.AsNoTracking().Where(x => x.IdServicio == s).Select(x => x.NombreServicio).FirstOrDefaultAsync(ct)
            : null;
        var familia = d.IdFamilia is int f
            ? await db.TiposEquipo.AsNoTracking().Where(x => x.IdFamilia == f).Select(x => x.NombreFamilia).FirstOrDefaultAsync(ct)
            : null;
        return $"{d.Desde:dd/MM/yyyy} al {d.Hasta:dd/MM/yyyy} · {servicio ?? "Todas las áreas"} · {familia ?? "Todos los tipos"}";
    }

    public async Task<DashboardDto> ObtenerAsync(DateOnly? desdeQ, DateOnly? hastaQ, int? idServicio, int? idFamilia, CancellationToken ct)
    {
        var hoy = Fechas.Hoy;
        var hasta = hastaQ ?? hoy;
        var desde = desdeQ ?? new DateOnly(hasta.Year, 1, 1);
        if (desde > hasta) throw new BusinessException("La fecha de inicio no puede ser posterior a la fecha de fin.");
        if (hasta.DayNumber - desde.DayNumber > 366 * 10) throw new BusinessException("El rango máximo es de 10 años.");

        var (desdeUtc, _) = Fechas.RangoDiaUtc(desde);
        var (_, hastaUtc) = Fechas.RangoDiaUtc(hasta);

        var umbral = (decimal)await parametros.GetIntAsync(Parametros.UmbralDisponibilidad, ct);
        var vidaUtil = await parametros.GetIntOrDefaultAsync(Parametros.VidaUtilAnios, 7, ct);
        var familiasResp = (await parametros.GetOrDefaultAsync(Parametros.FamiliasRespirador, "EMED,VAFO,VNIV", ct))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant()).ToHashSet();

        var servicios = await db.Servicios.AsNoTracking().OrderBy(s => s.IdServicio).ToListAsync(ct);
        var familias = await db.TiposEquipo.AsNoTracking().OrderBy(f => f.NombreFamilia).ToListAsync(ct);

        var equipos = await db.Equipos.AsNoTracking()
            .Select(e => new EqRow(e.IdEquipo, e.CodigoPatrimonial, e.IdFamilia, e.Familia.NombreFamilia, e.Familia.CodigoFamilia,
                e.Marca.NombreMarca, e.Modelo, e.IdServicioActual, e.ServicioActual.NombreServicio, e.EstadoOperativo,
                e.FechaAdquisicion, e.FechaMantenimientoPreventivo))
            .ToListAsync(ct);

        bool Pasa(EqRow e) => (idServicio is null || e.IdServicio == idServicio) && (idFamilia is null || e.IdFamilia == idFamilia);
        var eqPorId = equipos.ToDictionary(e => e.Id);
        var filtrados = equipos.Where(Pasa).ToList();
        var activos = filtrados.Where(e => e.Estado != EstadoEquipo.BAJA_PATRIMONIAL).ToList();

        var ots = (await db.OrdenesTrabajo.AsNoTracking()
                .Select(o => new OtRow(o.IdOt, o.IdEquipo, o.TipoMantenimiento, o.EstadoOt, o.FechaApertura, o.FechaCierre,
                    o.ResultadoSeguridadElectrica, o.ObservacionesSeguridadElectrica, o.IdInsumoFaltante,
                    o.ReporteFalla != null ? (DateTime?)o.ReporteFalla.FechaHoraReporte : null))
                .ToListAsync(ct))
            .Where(o => eqPorId.TryGetValue(o.IdEquipo, out var e) && Pasa(e))
            .ToList();

        var operativos = activos.Count(e => e.Estado == EstadoEquipo.OPERATIVO);
        var disponibilidad = activos.Count == 0 ? 100m : Math.Round(100m * operativos / activos.Count, 1);
        var respiradores = equipos.Where(e => e.Estado != EstadoEquipo.BAJA_PATRIMONIAL
                                              && familiasResp.Contains(e.CodigoFamilia)
                                              && (idServicio is null || e.IdServicio == idServicio)).ToList();
        var idsFiltrados = filtrados.Select(e => e.Id).ToHashSet();

        var kpis = new KpisDto(
            activos.Count, operativos, disponibilidad, disponibilidad < umbral ? "CRITICO" : "OK",
            respiradores.Count(e => e.Estado == EstadoEquipo.OPERATIVO), respiradores.Count,
            ots.Count(o => o.Estado != EstadoOt.FINALIZADA),
            (await db.ReportesFalla.AsNoTracking().Where(r => r.EstadoReporte == EstadoReporte.PENDIENTE)
                .Select(r => r.IdEquipo).ToListAsync(ct)).Count(idsFiltrados.Contains),
            await db.Alertas.CountAsync(a => !a.Resuelta, ct));

        var porTipo = familias.Where(f => idFamilia is null || f.IdFamilia == idFamilia).Select(f =>
        {
            var deFam = activos.Where(e => e.IdFamilia == f.IdFamilia).ToList();
            var op = deFam.Count(e => e.Estado == EstadoEquipo.OPERATIVO);
            var pct = deFam.Count == 0 ? 0 : Math.Round(100m * op / deFam.Count, 1);
            var sem = deFam.Count == 0 ? "SIN_DATOS" : pct < umbral ? "CRITICO" : "OK";
            return new DisponibilidadTipoDto(f.IdFamilia, f.NombreFamilia, deFam.Count, op, pct, sem);
        }).ToList();

        GrillaServicioDto Fila(int id, string nombre, List<EqRow> lista) => new(id, nombre,
            lista.Count(e => e.Estado == EstadoEquipo.OPERATIVO),
            lista.Count(e => e.Estado == EstadoEquipo.FUERA_DE_SERVICIO),
            lista.Count(e => e.Estado == EstadoEquipo.EN_MANTENIMIENTO),
            lista.Count(e => e.Estado == EstadoEquipo.EN_PRESTAMO),
            lista.Count);
        var grilla = servicios.Where(s => idServicio is null || s.IdServicio == idServicio)
            .Select(s => Fila(s.IdServicio, s.NombreServicio, activos.Where(e => e.IdServicio == s.IdServicio).ToList()))
            .ToList();
        var grillaTotal = Fila(0, "Total", activos);

        var insumos = await db.Insumos.AsNoTracking()
            .Select(i => new { i.IdInsumo, i.NombreInsumo, i.StockActual }).ToDictionaryAsync(i => i.IdInsumo, ct);
        var abiertas = ots.Where(o => o.Estado != EstadoOt.FINALIZADA).ToList();
        var porStock = abiertas.Where(o => o.IdInsumoFaltante != null).ToList();
        var porSeguridad = abiertas.Where(o => o.Estado == EstadoOt.EN_CURSO
                                               && o.Seguridad == ResultadoSeguridadElectrica.NO_APROBADA).ToList();
        string Desc(EqRow e) => $"{e.Familia} {e.Marca} {e.Modelo}";
        var bloqueadas = new OtsBloqueadasDto(porStock.Count, porSeguridad.Count,
            porStock.Select(o =>
                {
                    var e = eqPorId[o.IdEquipo];
                    var ins = insumos.GetValueOrDefault(o.IdInsumoFaltante!.Value);
                    return new OtBloqueadaDto(o.Id, e.Codigo, Desc(e), e.Servicio, "STOCK",
                        ins is null ? "Insumo faltante" : $"{ins.NombreInsumo} (stock actual {ins.StockActual})", o.Apertura);
                })
                .Concat(porSeguridad.Select(o =>
                {
                    var e = eqPorId[o.IdEquipo];
                    return new OtBloqueadaDto(o.Id, e.Codigo, Desc(e), e.Servicio, "SEGURIDAD_ELECTRICA",
                        string.IsNullOrWhiteSpace(o.ObsSeguridad) ? "Prueba de seguridad eléctrica no aprobada" : o.ObsSeguridad!, o.Apertura);
                }))
                .OrderBy(b => b.Apertura).ToList());

        var tareas = await TareasAsync(hoy, activos, abiertas, eqPorId, Pasa, idServicio is null && idFamilia is null, ct);

        var correctivas = ots.Where(o => o.Tipo == TipoMantenimiento.CORRECTIVO).ToList();
        var fallasPeriodo = correctivas.Where(o => o.Momento >= desdeUtc && o.Momento < hastaUtc).ToList();

        var fallasPorMarca = fallasPeriodo
            .GroupBy(o => eqPorId[o.IdEquipo].Marca)
            .Select(g => new ConteoDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Cantidad).ThenBy(c => c.Nombre).ToList();

        var cerradas = ots.Where(o => o.Estado == EstadoOt.FINALIZADA && o.Cierre >= desdeUtc && o.Cierre < hastaUtc
                                      && o.Tipo is TipoMantenimiento.CORRECTIVO or TipoMantenimiento.PREVENTIVO).ToList();
        double Dias(OtRow o) => (o.Cierre!.Value - (o.Tipo == TipoMantenimiento.CORRECTIVO ? o.Momento : o.Apertura)).TotalDays;
        decimal? Prom(IEnumerable<OtRow> l) { var x = l.ToList(); return x.Count == 0 ? null : Math.Round((decimal)x.Average(Dias), 1); }
        var mttr = new MttrDto(
            Prom(cerradas.Where(o => o.Tipo == TipoMantenimiento.CORRECTIVO)),
            Prom(cerradas.Where(o => o.Tipo == TipoMantenimiento.PREVENTIVO)),
            familias.Where(f => idFamilia is null || f.IdFamilia == idFamilia).Select(f =>
            {
                var c = cerradas.Where(o => eqPorId[o.IdEquipo].IdFamilia == f.IdFamilia && o.Tipo == TipoMantenimiento.CORRECTIVO).ToList();
                var p = cerradas.Where(o => eqPorId[o.IdEquipo].IdFamilia == f.IdFamilia && o.Tipo == TipoMantenimiento.PREVENTIVO).ToList();
                return new MttrFamiliaDto(f.NombreFamilia, Prom(c), c.Count, Prom(p), p.Count);
            }).ToList());

        var parque = await ParqueAsync(hoy, filtrados, vidaUtil, ct);

        var usos = (await db.UsosInsumo.AsNoTracking()
                .Where(u => u.FechaUso >= desdeUtc && u.FechaUso < hastaUtc)
                .Select(u => new { u.IdInsumo, u.CantidadConsumida, u.OrdenTrabajo.IdEquipo })
                .ToListAsync(ct))
            .Where(u => eqPorId.TryGetValue(u.IdEquipo, out var e) && Pasa(e)).ToList();
        var externos = ots.Where(o => o.Tipo == TipoMantenimiento.TERCERIZADO && o.Apertura >= desdeUtc && o.Apertura < hastaUtc).ToList();
        var consumo = new ConsumoRecursosDto(
            familias.Where(f => idFamilia is null || f.IdFamilia == idFamilia).Select(f => new ConsumoFamiliaDto(f.NombreFamilia,
                usos.Where(u => eqPorId[u.IdEquipo].IdFamilia == f.IdFamilia).Sum(u => u.CantidadConsumida),
                externos.Count(o => eqPorId[o.IdEquipo].IdFamilia == f.IdFamilia))).ToList(),
            usos.GroupBy(u => u.IdInsumo)
                .Select(g => new ConteoDto(insumos.TryGetValue(g.Key, out var i) ? i.NombreInsumo : $"Insumo {g.Key}", g.Sum(x => x.CantidadConsumida)))
                .OrderByDescending(c => c.Cantidad).Take(8).ToList(),
            usos.Sum(u => u.CantidadConsumida),
            externos.Count);

        var heatmap = Heatmap(hasta, correctivas, fallasPeriodo, familias.Where(f => idFamilia is null || f.IdFamilia == idFamilia)
            .Select(f => f.NombreFamilia).ToList(), eqPorId);

        return new DashboardDto(desde, hasta, idServicio, idFamilia, umbral, DateTime.UtcNow,
            kpis, porTipo, grilla, grillaTotal, bloqueadas, tareas, fallasPorMarca, mttr, parque, consumo, heatmap);
    }

    private async Task<TareasDiaDto> TareasAsync(DateOnly hoy, List<EqRow> activos, List<OtRow> abiertas,
        Dictionary<int, EqRow> eqPorId, Func<EqRow, bool> pasa, bool sinFiltros, CancellationToken ct)
    {
        var manana = hoy.AddDays(1);
        var semana = hoy.AddDays(7);

        var preventivos = activos
            .Where(e => e.Preventivo <= manana)
            .OrderBy(e => e.Preventivo)
            .Select(e =>
            {
                var otPrev = abiertas.FirstOrDefault(o => o.IdEquipo == e.Id && o.Tipo == TipoMantenimiento.PREVENTIVO);
                var dias = e.Preventivo.DayNumber - hoy.DayNumber;
                var detalle = $"{e.Familia} · {e.Servicio}" + (otPrev != null ? $" · OT #{otPrev.Id} abierta" : "");
                return new TareaDiaDto(e.Id, e.Id, e.Codigo, detalle, e.Preventivo, dias);
            }).ToList();

        var prestamosRaw = await db.Prestamos.AsNoTracking()
            .Where(p => p.EstadoPrestamo != EstadoPrestamo.FINALIZADO && p.FechaDevolucionEstimada <= semana)
            .OrderBy(p => p.FechaDevolucionEstimada)
            .Select(p => new
            {
                p.IdPrestamo, p.IdEquipo, p.TipoDireccion, p.FechaDevolucionEstimada, Hospital = p.HospitalExterno.NombreHospital,
                Externo = p.MarcaExterna + " " + p.ModeloExterno,
            })
            .ToListAsync(ct);
        var prestamos = prestamosRaw
            .Where(p => p.IdEquipo is int id ? eqPorId.TryGetValue(id, out var e) && pasa(e) : sinFiltros)
            .Select(p =>
            {
                var titulo = p.IdEquipo is int id ? eqPorId[id].Codigo : p.Externo;
                var sentido = p.TipoDireccion == TipoDireccionPrestamo.OTORGADO ? "Prestado a" : "Recibido de";
                return new TareaDiaDto(p.IdPrestamo, p.IdEquipo, titulo, $"{sentido} {p.Hospital}", p.FechaDevolucionEstimada,
                    p.FechaDevolucionEstimada.DayNumber - hoy.DayNumber);
            }).ToList();

        var idsAbiertas = abiertas.Select(o => o.Id).ToHashSet();
        var tercRaw = await db.Tercerizados.AsNoTracking()
            .Where(t => t.FechaVueltaReal == null && t.FechaProbableVuelta <= semana)
            .OrderBy(t => t.FechaProbableVuelta)
            .Select(t => new { t.IdOt, t.ProveedorFabricaNombre, t.FechaProbableVuelta, t.EnGarantia, t.OrdenTrabajo.IdEquipo })
            .ToListAsync(ct);
        var tercerizados = tercRaw
            .Where(t => idsAbiertas.Contains(t.IdOt))
            .Select(t => new TareaDiaDto(t.IdOt, t.IdEquipo, eqPorId[t.IdEquipo].Codigo,
                $"{t.ProveedorFabricaNombre}{(t.EnGarantia ? " · garantía" : "")} · OT #{t.IdOt}",
                t.FechaProbableVuelta, t.FechaProbableVuelta.DayNumber - hoy.DayNumber))
            .ToList();

        return new TareasDiaDto(preventivos, prestamos, tercerizados);
    }

    private async Task<ParqueDto> ParqueAsync(DateOnly hoy, List<EqRow> filtrados, int vidaUtil, CancellationToken ct)
    {
        var bajas = await db.BajasPatrimoniales.AsNoTracking()
            .Select(b => new { b.IdEquipo, b.FechaBaja }).ToDictionaryAsync(b => b.IdEquipo, b => b.FechaBaja, ct);

        if (filtrados.Count == 0) return new ParqueDto(vidaUtil, new(), 0, 0, 0, 0);

        var primerAnio = Math.Max(filtrados.Min(e => e.Adquisicion.Year), hoy.Year - 14);
        var serie = new List<ParqueAnioDto>();
        for (var anio = primerAnio; anio <= hoy.Year; anio++)
        {
            var corte = anio == hoy.Year ? hoy : new DateOnly(anio, 12, 31);
            var enServicio = filtrados.Where(e => e.Adquisicion <= corte
                && (!bajas.TryGetValue(e.Id, out var fb) || fb > corte)).ToList();
            var excedidos = enServicio.Count(e => e.Adquisicion.AddYears(vidaUtil) < corte);
            serie.Add(new ParqueAnioDto(anio, enServicio.Count - excedidos, excedidos));
        }

        var actuales = filtrados.Where(e => e.Estado != EstadoEquipo.BAJA_PATRIMONIAL).ToList();
        var excedidosHoy = actuales.Count(e => e.Adquisicion.AddYears(vidaUtil) < hoy);
        var edadProm = actuales.Count == 0 ? 0 : Math.Round((decimal)actuales.Average(e => (hoy.DayNumber - e.Adquisicion.DayNumber) / 365.25), 1);
        return new ParqueDto(vidaUtil, serie, actuales.Count, excedidosHoy,
            actuales.Count == 0 ? 0 : Math.Round(100m * excedidosHoy / actuales.Count, 1), edadProm);
    }

    private static HeatmapDto Heatmap(DateOnly hasta, List<OtRow> correctivas, List<OtRow> fallasPeriodo,
        List<string> familias, Dictionary<int, EqRow> eqPorId)
    {
        var primerMes = new DateTime(hasta.Year, hasta.Month, 1).AddMonths(-11);
        var (desde12, _) = Fechas.RangoMesUtc(primerMes.Year, primerMes.Month);
        var (_, hasta12) = Fechas.RangoMesUtc(hasta.Year, hasta.Month);
        var meses = Enumerable.Range(0, 12).Select(i => primerMes.AddMonths(i).ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList();

        var ventana = correctivas.Where(o => o.Momento >= desde12 && o.Momento < hasta12)
            .Select(o => (Familia: eqPorId[o.IdEquipo].Familia, Local: Fechas.ALocal(o.Momento))).ToList();

        var conteo = ventana.GroupBy(x => (x.Familia, Mes: x.Local.ToString("yyyy-MM", CultureInfo.InvariantCulture)))
            .ToDictionary(g => g.Key, g => g.Count());
        var celdas = (from f in familias from m in meses select new HeatmapCeldaDto(f, m, conteo.GetValueOrDefault((f, m)))).ToList();

        var porMes = ventana.GroupBy(x => new { x.Local.Year, x.Local.Month }).Select(g => new { g.Key.Year, g.Key.Month, N = g.Count() })
            .OrderByDescending(x => x.N).ThenByDescending(x => x.Year).ThenByDescending(x => x.Month).FirstOrDefault();
        var invierno = ventana.Count == 0 ? 0 : Math.Round(100m * ventana.Count(x => x.Local.Month is 6 or 7 or 8) / ventana.Count, 1);

        var diaHora = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();
        foreach (var o in fallasPeriodo)
        {
            var l = Fechas.ALocal(o.Momento);
            diaHora[((int)l.DayOfWeek + 6) % 7][l.Hour]++;
        }
        int? horaPico = null, diaPico = null;
        if (fallasPeriodo.Count > 0)
        {
            var porHora = Enumerable.Range(0, 24).Select(h => diaHora.Sum(d => d[h])).ToArray();
            horaPico = Array.IndexOf(porHora, porHora.Max());
            var porDia = diaHora.Select(d => d.Sum()).ToArray();
            diaPico = Array.IndexOf(porDia, porDia.Max());
        }

        return new HeatmapDto(meses, familias, celdas, diaHora, fallasPeriodo.Count, horaPico, diaPico,
            porMes is null ? null : $"{Fechas.MesesEs[porMes.Month]} {porMes.Year}", porMes?.N ?? 0, invierno, ventana.Count);
    }
}
