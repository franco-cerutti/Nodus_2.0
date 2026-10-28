using Microsoft.EntityFrameworkCore;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;

namespace Nodus.Api.Services;

public class AuthService(NodusDbContext db, TokenService tokens)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        var u = await db.Usuarios.Include(x => x.Rol).Include(x => x.Servicio)
            .FirstOrDefaultAsync(x => x.NombreUsuario == r.NombreUsuario.Trim(), ct);

        if (u is null || !u.Activo || !BCrypt.Net.BCrypt.Verify(r.Password, u.PasswordHash))
            throw new BusinessException("Usuario o contraseña incorrectos.", StatusCodes.Status401Unauthorized);

        var (token, expira) = tokens.Create(u);
        return new LoginResponse(token, expira, ToSesion(u));
    }

    public async Task<UsuarioSesionDto> MeAsync(int id, CancellationToken ct)
    {
        var u = await db.Usuarios.AsNoTracking().Include(x => x.Rol).Include(x => x.Servicio)
                    .FirstOrDefaultAsync(x => x.IdUsuario == id && x.Activo, ct)
                ?? throw new BusinessException("Sesión inválida", StatusCodes.Status401Unauthorized);
        return ToSesion(u);
    }

    private static UsuarioSesionDto ToSesion(Usuario u) =>
        new(u.IdUsuario, u.NombreUsuario, u.NombreCompleto, u.Rol.NombreRol, u.IdServicio, u.Servicio?.NombreServicio);
}

public class CatalogoService(NodusDbContext db)
{
    public async Task<CatalogosDto> ObtenerAsync(CancellationToken ct) => new(
        await db.Servicios.AsNoTracking().OrderBy(s => s.IdServicio)
            .Select(s => new ItemDto(s.IdServicio, s.NombreServicio, s.CodigoServicio)).ToListAsync(ct),
        await db.TiposEquipo.AsNoTracking().OrderBy(f => f.NombreFamilia)
            .Select(f => new ItemDto(f.IdFamilia, f.NombreFamilia, f.CodigoFamilia)).ToListAsync(ct),
        await db.Marcas.AsNoTracking().OrderBy(m => m.NombreMarca)
            .Select(m => new ItemDto(m.IdMarca, m.NombreMarca, null)).ToListAsync(ct),
        await db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && (u.Rol.NombreRol == Roles.Tecnico || u.Rol.NombreRol == Roles.Bioingeniero))
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new ItemDto(u.IdUsuario, u.NombreCompleto, u.Legajo)).ToListAsync(ct));

    public async Task<ItemDto> CrearMarcaAsync(CrearMarcaRequest r, CancellationToken ct)
    {
        var m = new Marca { NombreMarca = r.Nombre.Trim() };
        db.Marcas.Add(m);
        await db.SaveChangesAsync(ct);
        return new ItemDto(m.IdMarca, m.NombreMarca);
    }
}

public class AlertaService(NodusDbContext db)
{
    public Task<List<AlertaDto>> ListarAsync(bool soloAbiertas, CancellationToken ct)
    {
        var q = db.Alertas.AsNoTracking();
        if (soloAbiertas) q = q.Where(a => !a.Resuelta);
        return q.OrderBy(a => a.Leida).ThenByDescending(a => a.FechaGeneracion).Take(500)
            .Select(a => new AlertaDto(a.IdAlerta, a.TipoAlerta, a.IdReferencia, a.Mensaje, a.FechaGeneracion, a.Leida, a.Resuelta))
            .ToListAsync(ct);
    }

    public async Task MarcarLeidaAsync(int id, CancellationToken ct)
    {
        var n = await db.Alertas.Where(a => a.IdAlerta == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Leida, true), ct);
        if (n == 0) throw new NotFoundException($"No existe la alerta {id}");
    }
}

/// <summary>Gestión de usuarios y parámetros del sistema.</summary>
public class AdminService(NodusDbContext db, ICurrentUser user)
{
    private static IQueryable<UsuarioDto> Proyectar(IQueryable<Usuario> q) =>
        q.Select(u => new UsuarioDto(u.IdUsuario, u.NombreUsuario, u.Legajo, u.NombreCompleto, u.IdRol, u.Rol.NombreRol,
            u.IdServicio, u.Servicio != null ? u.Servicio.NombreServicio : null, u.Activo, u.CreatedAt));

    public Task<List<UsuarioDto>> ListarUsuariosAsync(CancellationToken ct) =>
        Proyectar(db.Usuarios.AsNoTracking().OrderBy(u => u.IdRol).ThenBy(u => u.NombreUsuario)).ToListAsync(ct);

    public Task<List<ItemDto>> RolesAsync(CancellationToken ct) =>
        db.Roles.AsNoTracking().OrderBy(r => r.IdRol).Select(r => new ItemDto(r.IdRol, r.NombreRol, null)).ToListAsync(ct);

    public async Task<UsuarioDto> CrearUsuarioAsync(CrearUsuarioRequest r, CancellationToken ct)
    {
        await ValidarRolServicioAsync(r.IdRol, r.IdServicio, ct);
        var u = new Usuario
        {
            NombreUsuario = r.NombreUsuario.Trim().ToLowerInvariant(),
            Legajo = string.IsNullOrWhiteSpace(r.Legajo) ? null : r.Legajo.Trim(),
            NombreCompleto = r.NombreCompleto.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password),
            IdRol = r.IdRol,
            IdServicio = r.IdServicio,
            Activo = true,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync(ct);
        return await Proyectar(db.Usuarios.AsNoTracking().Where(x => x.IdUsuario == u.IdUsuario)).FirstAsync(ct);
    }

    public async Task<UsuarioDto> ActualizarUsuarioAsync(int id, ActualizarUsuarioRequest r, CancellationToken ct)
    {
        var u = await db.Usuarios.Include(x => x.Rol).FirstOrDefaultAsync(x => x.IdUsuario == id, ct)
                ?? throw new NotFoundException($"No existe el usuario {id}");

        var rolNuevo = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.IdRol == r.IdRol, ct)
                       ?? throw new BusinessException("El rol indicado no existe.");

        if (user.Rol != Roles.Bioingeniero &&
            (u.Rol.NombreRol == Roles.Bioingeniero || rolNuevo.NombreRol == Roles.Bioingeniero))
            throw new BusinessException("Solo un Bioingeniero puede modificar cuentas o asignar el rol Bioingeniero.",
                StatusCodes.Status403Forbidden);

        if (id == user.Id && (!r.Activo || rolNuevo.NombreRol != u.Rol.NombreRol))
            throw new BusinessException("No podés desactivar tu propia cuenta ni cambiar tu propio rol.");

        if (u.Rol.NombreRol == Roles.Bioingeniero && (!r.Activo || rolNuevo.NombreRol != Roles.Bioingeniero))
        {
            var otrosBio = await db.Usuarios.CountAsync(x => x.IdUsuario != id && x.Activo && x.Rol.NombreRol == Roles.Bioingeniero, ct);
            if (otrosBio == 0) throw new BusinessException("Debe quedar al menos un Bioingeniero activo.");
        }

        await ValidarRolServicioAsync(r.IdRol, r.IdServicio, ct);

        u.Legajo = string.IsNullOrWhiteSpace(r.Legajo) ? null : r.Legajo.Trim();
        u.NombreCompleto = r.NombreCompleto.Trim();
        u.IdRol = r.IdRol;
        u.IdServicio = r.IdServicio;
        u.Activo = r.Activo;
        if (!string.IsNullOrWhiteSpace(r.NuevaPassword))
            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.NuevaPassword);
        await db.SaveChangesAsync(ct);
        return await Proyectar(db.Usuarios.AsNoTracking().Where(x => x.IdUsuario == id)).FirstAsync(ct);
    }

    private async Task ValidarRolServicioAsync(int idRol, int? idServicio, CancellationToken ct)
    {
        var rol = await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.IdRol == idRol, ct)
                  ?? throw new BusinessException("El rol indicado no existe.");
        if (rol.NombreRol == Roles.Asistencial && idServicio is null)
            throw new BusinessException("Un usuario de Personal Asistencial debe estar vinculado a un servicio.");
        if (rol.NombreRol != Roles.Asistencial && idServicio is not null)
            throw new BusinessException("Solo los usuarios de Personal Asistencial se vinculan a un servicio.");
        if (idServicio is int s && !await db.Servicios.AnyAsync(x => x.IdServicio == s, ct))
            throw new BusinessException("El servicio indicado no existe.");
    }

    public Task<List<ParametroDto>> ParametrosAsync(CancellationToken ct) =>
        db.Parametros.AsNoTracking().OrderBy(p => p.Clave).Select(p => new ParametroDto(p.Clave, p.Valor)).ToListAsync(ct);

    public async Task<ParametroDto> ActualizarParametroAsync(string clave, string valor, CancellationToken ct)
    {
        var p = await db.Parametros.FirstOrDefaultAsync(x => x.Clave == clave, ct)
                ?? throw new NotFoundException($"No existe el parámetro {clave}");

        valor = valor.Trim();
        if (clave == Parametros.CodigoHospital)
        {
            if (valor.Length is < 2 or > 10 || !valor.All(char.IsAsciiLetterOrDigit))
                throw new BusinessException("El código de hospital debe tener entre 2 y 10 letras o dígitos ASCII.");
            valor = valor.ToUpperInvariant();
        }
        else if (clave == Parametros.FamiliasRespirador)
        {
            var codigos = valor.ToUpperInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            var existentes = await db.TiposEquipo.AsNoTracking().Select(f => f.CodigoFamilia).ToListAsync(ct);
            var invalidos = codigos.Where(c => !existentes.Contains(c)).ToList();
            if (codigos.Count == 0 || invalidos.Count > 0)
                throw new BusinessException($"Indicá códigos de familia separados por coma. Válidos: {string.Join(", ", existentes)}." +
                                            (invalidos.Count > 0 ? $" Inexistentes: {string.Join(", ", invalidos)}." : ""));
            valor = string.Join(',', codigos);
        }
        else if (!int.TryParse(valor, out var n) || n < 0
                 || (clave == Parametros.UmbralDisponibilidad && n > 100)
                 || (clave == Parametros.VidaUtilAnios && n is < 1 or > 50))
            throw new BusinessException("El valor debe ser un número entero válido (umbral: 0 a 100; vida útil: 1 a 50 años).");

        p.Valor = valor;
        await db.SaveChangesAsync(ct);
        return new ParametroDto(p.Clave, p.Valor);
    }
}
