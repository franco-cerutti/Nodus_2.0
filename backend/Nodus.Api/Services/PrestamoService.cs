using Microsoft.EntityFrameworkCore;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;

namespace Nodus.Api.Services;

/// <summary>Préstamos interinstitucionales (otorgados y recibidos) y hospitales externos.</summary>
public class PrestamoService(NodusDbContext db, ICurrentUser user)
{
    private static readonly EstadoPrestamo[] Activos = { EstadoPrestamo.EN_PRESTAMO, EstadoPrestamo.VENCIDO, EstadoPrestamo.PRORROGA };

    public Task<List<HospitalExternoDto>> ListarHospitalesAsync(CancellationToken ct) =>
        db.HospitalesExternos.AsNoTracking().OrderBy(h => h.NombreHospital)
            .Select(h => new HospitalExternoDto(h.IdHospitalExterno, h.NombreHospital, h.Direccion, h.Latitud, h.Longitud,
                db.Prestamos.Count(p => p.IdHospitalExterno == h.IdHospitalExterno && p.EstadoPrestamo != EstadoPrestamo.FINALIZADO)))
            .ToListAsync(ct);

    public async Task<HospitalExternoDto> GuardarHospitalAsync(int? id, HospitalExternoRequest r, CancellationToken ct)
    {
        HospitalExterno h;
        if (id is int existente)
            h = await db.HospitalesExternos.FirstOrDefaultAsync(x => x.IdHospitalExterno == existente, ct)
                ?? throw new NotFoundException($"No existe el hospital {existente}");
        else
            db.HospitalesExternos.Add(h = new HospitalExterno());

        h.NombreHospital = r.Nombre.Trim();
        h.Direccion = r.Direccion?.Trim();
        h.Latitud = r.Latitud;
        h.Longitud = r.Longitud;
        await db.SaveChangesAsync(ct);
        return (await ListarHospitalesAsync(ct)).First(x => x.Id == h.IdHospitalExterno);
    }

    private static IQueryable<PrestamoDto> Proyectar(IQueryable<PrestamoEquipo> q) =>
        q.Select(p => new PrestamoDto(
            p.IdPrestamo, p.TipoDireccion, p.EstadoPrestamo,
            p.IdEquipo, p.Equipo != null ? p.Equipo.CodigoPatrimonial : null,
            p.Equipo != null
                ? p.Equipo.Familia.NombreFamilia + " " + p.Equipo.Marca.NombreMarca + " " + p.Equipo.Modelo + " (S/N " + p.Equipo.NroSerie + ")"
                : p.MarcaExterna + " " + p.ModeloExterno + (p.NroSerieExterno != null ? " (S/N " + p.NroSerieExterno + ")" : ""),
            p.IdHospitalExterno, p.HospitalExterno.NombreHospital,
            p.ResponsableRetiroEntrega, p.FechaSalida, p.FechaDevolucionEstimada, p.FechaDevolucionReal,
            p.EstadoTecnicoEntrega, p.EstadoTecnicoRecepcion, p.AccesoriosPrestados, p.Usuario.NombreCompleto));

    public Task<List<PrestamoDto>> ListarAsync(EstadoPrestamo? estado, TipoDireccionPrestamo? tipo, int? idEquipo, CancellationToken ct)
    {
        var q = db.Prestamos.AsNoTracking();
        if (estado is EstadoPrestamo e) q = q.Where(p => p.EstadoPrestamo == e);
        if (tipo is TipoDireccionPrestamo t) q = q.Where(p => p.TipoDireccion == t);
        if (idEquipo is int id) q = q.Where(p => p.IdEquipo == id);
        return Proyectar(q.OrderByDescending(p => p.FechaSalida).ThenByDescending(p => p.IdPrestamo)).ToListAsync(ct);
    }

    public async Task<PrestamoDto> ObtenerAsync(int id, CancellationToken ct) =>
        await Proyectar(db.Prestamos.AsNoTracking().Where(p => p.IdPrestamo == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"No existe el préstamo {id}");

    public async Task<PrestamoDto> OtorgarAsync(CrearPrestamoOtorgadoRequest r, CancellationToken ct)
    {
        ValidarFechas(r.FechaSalida, r.FechaDevolucionEstimada);
        var p = new PrestamoEquipo
        {
            TipoDireccion = TipoDireccionPrestamo.OTORGADO,
            IdEquipo = r.IdEquipo,
            IdHospitalExterno = r.IdHospitalExterno,
            ResponsableRetiroEntrega = r.Responsable.Trim(),
            FechaSalida = r.FechaSalida,
            FechaDevolucionEstimada = r.FechaDevolucionEstimada,
            EstadoPrestamo = EstadoPrestamo.EN_PRESTAMO,
            EstadoTecnicoEntrega = r.EstadoTecnicoEntrega.Trim(),
            AccesoriosPrestados = r.Accesorios?.Trim(),
            IdUsuario = user.Id,
        };
        db.Prestamos.Add(p);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(p.IdPrestamo, ct);
    }

    public async Task<PrestamoDto> RecibirAsync(CrearPrestamoRecibidoRequest r, CancellationToken ct)
    {
        ValidarFechas(r.FechaSalida, r.FechaDevolucionEstimada);
        var p = new PrestamoEquipo
        {
            TipoDireccion = TipoDireccionPrestamo.RECIBIDO,
            IdEquipo = null,
            MarcaExterna = r.MarcaExterna.Trim(),
            ModeloExterno = r.ModeloExterno.Trim(),
            NroSerieExterno = string.IsNullOrWhiteSpace(r.NroSerieExterno) ? null : r.NroSerieExterno.Trim(),
            IdHospitalExterno = r.IdHospitalExterno,
            ResponsableRetiroEntrega = r.Responsable.Trim(),
            FechaSalida = r.FechaSalida,
            FechaDevolucionEstimada = r.FechaDevolucionEstimada,
            EstadoPrestamo = EstadoPrestamo.EN_PRESTAMO,
            EstadoTecnicoEntrega = r.EstadoTecnicoEntrega.Trim(),
            AccesoriosPrestados = r.Accesorios?.Trim(),
            IdUsuario = user.Id,
        };
        db.Prestamos.Add(p);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(p.IdPrestamo, ct);
    }

    public async Task<PrestamoDto> ProrrogarAsync(int id, ProrrogaRequest r, CancellationToken ct)
    {
        var p = await CargarActivoAsync(id, ct);
        if (r.NuevaFechaDevolucion <= p.FechaDevolucionEstimada)
            throw new BusinessException("La nueva fecha debe ser posterior a la fecha de devolución estimada actual.");
        if (r.NuevaFechaDevolucion < Fechas.Hoy)
            throw new BusinessException("La nueva fecha de devolución no puede estar en el pasado.");

        p.FechaDevolucionEstimada = r.NuevaFechaDevolucion;
        p.EstadoPrestamo = EstadoPrestamo.PRORROGA;
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<PrestamoDto> FinalizarAsync(int id, FinalizarPrestamoRequest r, CancellationToken ct)
    {
        var p = await CargarActivoAsync(id, ct);
        if (r.FechaDevolucionReal > Fechas.Hoy)
            throw new BusinessException("La fecha de devolución real no puede ser futura.");

        p.FechaDevolucionReal = r.FechaDevolucionReal;
        p.EstadoTecnicoRecepcion = r.EstadoTecnicoRecepcion.Trim();
        p.EstadoPrestamo = EstadoPrestamo.FINALIZADO;
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    private async Task<PrestamoEquipo> CargarActivoAsync(int id, CancellationToken ct)
    {
        var p = await db.Prestamos.FirstOrDefaultAsync(x => x.IdPrestamo == id, ct)
                ?? throw new NotFoundException($"No existe el préstamo {id}");
        if (!Activos.Contains(p.EstadoPrestamo))
            throw new BusinessException("El préstamo ya está finalizado.", StatusCodes.Status409Conflict);
        return p;
    }

    private static void ValidarFechas(DateOnly salida, DateOnly devolucion)
    {
        if (devolucion < salida)
            throw new BusinessException("La fecha de devolución estimada no puede ser anterior a la de salida.");
    }
}
