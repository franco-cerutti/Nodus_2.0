using Microsoft.EntityFrameworkCore;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;

namespace Nodus.Api.Services;

/// <summary>Insumos, reposiciones y solicitudes de compra.</summary>
public class InsumoService(NodusDbContext db, ICurrentUser user)
{
    private static IQueryable<InsumoDto> Proyectar(IQueryable<Insumo> q) =>
        q.Select(i => new InsumoDto(i.IdInsumo, i.NombreInsumo, i.Categoria, i.StockActual, i.StockMinimo, i.EstadoStock));

    public Task<List<InsumoDto>> ListarAsync(EstadoStock? estado, CancellationToken ct)
    {
        var q = db.Insumos.AsNoTracking();
        if (estado is EstadoStock e) q = q.Where(i => i.EstadoStock == e);
        return Proyectar(q.OrderBy(i => i.Categoria).ThenBy(i => i.NombreInsumo)).ToListAsync(ct);
    }

    public async Task<InsumoDto> ObtenerAsync(int id, CancellationToken ct) =>
        await Proyectar(db.Insumos.AsNoTracking().Where(i => i.IdInsumo == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"No existe el insumo {id}");

    public async Task<InsumoDto> CrearAsync(CrearInsumoRequest r, CancellationToken ct)
    {
        var i = new Insumo
        {
            NombreInsumo = r.Nombre.Trim(),
            Categoria = r.Categoria.Trim(),
            StockActual = r.StockInicial,
            StockMinimo = r.StockMinimo,
        };
        db.Insumos.Add(i);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(i.IdInsumo, ct);
    }

    /// <summary>Actualiza datos y stock mínimo del insumo.</summary>
    public async Task<InsumoDto> ActualizarAsync(int id, ActualizarInsumoRequest r, CancellationToken ct)
    {
        var i = await db.Insumos.FirstOrDefaultAsync(x => x.IdInsumo == id, ct)
                ?? throw new NotFoundException($"No existe el insumo {id}");
        i.NombreInsumo = r.Nombre.Trim();
        i.Categoria = r.Categoria.Trim();
        i.StockMinimo = r.StockMinimo;
        await db.SaveChangesAsync(ct);

        await db.Alertas
            .Where(a => a.TipoAlerta == TipoAlerta.STOCK_BAJO && a.IdReferencia == id && !a.Resuelta
                        && db.Insumos.Any(x => x.IdInsumo == id && x.EstadoStock == EstadoStock.STOCK_NORMAL))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Resuelta, true), ct);

        return await ObtenerAsync(id, ct);
    }

    public async Task<InsumoDto> ReponerAsync(int id, int cantidad, CancellationToken ct)
    {
        if (!await db.Insumos.AnyAsync(i => i.IdInsumo == id, ct))
            throw new NotFoundException($"No existe el insumo {id}");
        db.Reposiciones.Add(new ReposicionInsumo
        {
            IdInsumo = id,
            CantidadIngresada = cantidad,
            FechaReposicion = DateTime.UtcNow,
            IdUsuario = user.Id,
        });
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<List<MovimientoInsumoDto>> MovimientosAsync(int id, CancellationToken ct)
    {
        var consumos = await db.UsosInsumo.AsNoTracking().Where(u => u.IdInsumo == id)
            .Select(u => new MovimientoInsumoDto(u.FechaUso, "CONSUMO", -u.CantidadConsumida,
                "OT #" + u.IdOt + " · " + u.OrdenTrabajo.Equipo.CodigoPatrimonial, u.Usuario.NombreCompleto))
            .ToListAsync(ct);
        var repos = await db.Reposiciones.AsNoTracking().Where(r => r.IdInsumo == id)
            .Select(r => new MovimientoInsumoDto(r.FechaReposicion, "REPOSICION", r.CantidadIngresada, "Reposición", r.Usuario.NombreCompleto))
            .ToListAsync(ct);
        return consumos.Concat(repos).OrderByDescending(m => m.Fecha).ToList();
    }

    private static IQueryable<SolicitudCompraDto> ProyectarSolicitudes(IQueryable<SolicitudCompraInsumo> q) =>
        q.Select(s => new SolicitudCompraDto(s.IdSolicitud, s.IdInsumo, s.Insumo.NombreInsumo, s.CantidadAComprar,
            s.FechaSolicitud, s.Usuario.NombreCompleto));

    public async Task<SolicitudCompraDto> SolicitarCompraAsync(int idInsumo, int cantidad, CancellationToken ct)
    {
        if (!await db.Insumos.AnyAsync(i => i.IdInsumo == idInsumo, ct))
            throw new NotFoundException($"No existe el insumo {idInsumo}");
        var s = new SolicitudCompraInsumo
        {
            IdInsumo = idInsumo,
            CantidadAComprar = cantidad,
            FechaSolicitud = DateTime.UtcNow,
            IdUsuario = user.Id,
        };
        db.SolicitudesCompra.Add(s);
        await db.SaveChangesAsync(ct);
        return await ObtenerSolicitudAsync(s.IdSolicitud, ct);
    }

    public Task<List<SolicitudCompraDto>> ListarSolicitudesAsync(CancellationToken ct) =>
        ProyectarSolicitudes(db.SolicitudesCompra.AsNoTracking().OrderByDescending(s => s.FechaSolicitud)).ToListAsync(ct);

    public async Task<SolicitudCompraDto> ObtenerSolicitudAsync(int id, CancellationToken ct) =>
        await ProyectarSolicitudes(db.SolicitudesCompra.AsNoTracking().Where(s => s.IdSolicitud == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"No existe la solicitud de compra {id}");
}
