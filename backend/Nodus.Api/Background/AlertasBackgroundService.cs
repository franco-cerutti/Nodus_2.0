using Microsoft.EntityFrameworkCore;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Nodus.Api.Infrastructure;

namespace Nodus.Api.Background;

public class AlertasOptions
{
    /// <summary>Hora local (Córdoba) de la corrida diaria.</summary>
    public int HoraEjecucion { get; set; } = 6;
    /// <summary>Ejecuta una corrida al iniciar la API.</summary>
    public bool EjecutarAlIniciar { get; set; } = true;
}

/// <summary>Generación diaria de alertas de preventivos, préstamos y tercerizados vencidos.</summary>
public class AlertasBackgroundService(IServiceScopeFactory scopes, ILogger<AlertasBackgroundService> log,
    Microsoft.Extensions.Options.IOptions<AlertasOptions> opt) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (opt.Value.EjecutarAlIniciar)
            await EjecutarSeguroAsync(stop);

        while (!stop.IsCancellationRequested)
        {
            var ahora = Fechas.AhoraLocal;
            var proxima = ahora.Date.AddHours(opt.Value.HoraEjecucion);
            if (proxima <= ahora) proxima = proxima.AddDays(1);
            var espera = proxima - ahora;
            log.LogInformation("Próxima corrida de alertas: {Proxima:dd/MM/yyyy HH:mm} (hora local)", proxima);

            try { await Task.Delay(espera, stop); }
            catch (TaskCanceledException) { break; }

            await EjecutarSeguroAsync(stop);
        }
    }

    private async Task EjecutarSeguroAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NodusDbContext>();
            var parametros = scope.ServiceProvider.GetRequiredService<ParametroService>();
            var r = await EjecutarAsync(db, parametros, ct);
            log.LogInformation("Alertas generadas — preventivo: {P}, préstamos vencidos: {V} (+{E} préstamos marcados VENCIDO), tercerizados: {T}, stock: {S}",
                r.preventivos, r.prestamos, r.prestamosVencidos, r.tercerizados, r.stock);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Falló la corrida de alertas; se reintenta en la próxima ejecución");
        }
    }

    public static async Task<(int preventivos, int prestamosVencidos, int prestamos, int tercerizados, int stock)> EjecutarAsync(
        NodusDbContext db, ParametroService parametros, CancellationToken ct)
    {
        var diasPreventivo = await parametros.GetIntAsync(Parametros.DiasAlertaPreventivo, ct);
        var hoy = Fechas.Hoy;
        var limite = hoy.AddDays(diasPreventivo);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var preventivos = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO alerta (tipo_alerta, id_referencia, mensaje)
            SELECT 'PREVENTIVO_PROXIMO', e.id_equipo,
                   format('Mantenimiento preventivo %s del equipo %s (%s): %s',
                          CASE WHEN e.fecha_mantenimiento_preventivo < {hoy} THEN 'VENCIDO' ELSE 'próximo' END,
                          e.codigo_patrimonial, s.nombre_servicio, to_char(e.fecha_mantenimiento_preventivo, 'DD/MM/YYYY'))
              FROM equipo e
              JOIN servicio s ON s.id_servicio = e.id_servicio_actual
             WHERE e.estado_operativo <> 'BAJA_PATRIMONIAL'
               AND e.fecha_mantenimiento_preventivo <= {limite}
            ON CONFLICT DO NOTHING", ct);

        var vencidos = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE prestamo_equipo SET estado_prestamo = 'VENCIDO'
             WHERE estado_prestamo IN ('EN_PRESTAMO', 'PRORROGA')
               AND fecha_devolucion_estimada < {hoy}", ct);

        var prestamos = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO alerta (tipo_alerta, id_referencia, mensaje)
            SELECT 'PRESTAMO_VENCIDO', p.id_prestamo,
                   format('Préstamo %s N° %s con %s vencido el %s: %s',
                          lower(p.tipo_direccion), p.id_prestamo, h.nombre_hospital,
                          to_char(p.fecha_devolucion_estimada, 'DD/MM/YYYY'),
                          COALESCE(e.codigo_patrimonial, p.marca_externa || ' ' || p.modelo_externo))
              FROM prestamo_equipo p
              JOIN hospital_externo h ON h.id_hospital_externo = p.id_hospital_externo
              LEFT JOIN equipo e ON e.id_equipo = p.id_equipo
             WHERE p.estado_prestamo = 'VENCIDO'
            ON CONFLICT DO NOTHING", ct);

        var tercerizados = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO alerta (tipo_alerta, id_referencia, mensaje)
            SELECT 'TERCERIZADO_VENCIDO', d.id_tercerizado,
                   format('El equipo %s enviado a %s (OT #%s) debía volver el %s',
                          e.codigo_patrimonial, d.proveedor_fabrica_nombre, o.id_ot, to_char(d.fecha_probable_vuelta, 'DD/MM/YYYY'))
              FROM detalle_mantenimiento_tercerizado d
              JOIN orden_trabajo o ON o.id_ot = d.id_ot
              JOIN equipo e ON e.id_equipo = o.id_equipo
             WHERE d.fecha_vuelta_real IS NULL
               AND o.estado_ot <> 'FINALIZADA'
               AND d.fecha_probable_vuelta < {hoy}
            ON CONFLICT DO NOTHING", ct);

        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE alerta a SET resuelta = TRUE
              FROM detalle_mantenimiento_tercerizado d
              JOIN orden_trabajo o ON o.id_ot = d.id_ot
             WHERE a.tipo_alerta = 'TERCERIZADO_VENCIDO' AND a.resuelta = FALSE
               AND a.id_referencia = d.id_tercerizado
               AND (d.fecha_vuelta_real IS NOT NULL OR o.estado_ot = 'FINALIZADA')", ct);

        var stock = await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO alerta (tipo_alerta, id_referencia, mensaje)
            SELECT 'STOCK_BAJO', i.id_insumo,
                   format('Insumo ""%s"" con stock %s (mínimo %s)', i.nombre_insumo, i.stock_actual, i.stock_minimo)
              FROM insumo i
             WHERE i.estado_stock IN ('STOCK_BAJO', 'AGOTADO')
            ON CONFLICT DO NOTHING", ct);

        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE alerta a SET resuelta = TRUE
              FROM insumo i
             WHERE a.tipo_alerta = 'STOCK_BAJO' AND a.resuelta = FALSE
               AND a.id_referencia = i.id_insumo AND i.estado_stock = 'STOCK_NORMAL'", ct);

        await tx.CommitAsync(ct);
        return (preventivos, vencidos, prestamos, tercerizados, stock);
    }
}
