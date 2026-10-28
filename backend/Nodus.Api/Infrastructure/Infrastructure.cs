using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nodus.Api.Data;
using Nodus.Api.Domain;
using Npgsql;

namespace Nodus.Api.Infrastructure;

/// <summary>Excepción de negocio detectada en la API (validaciones que no resuelve la base).</summary>
public class BusinessException(string message, int status = StatusCodes.Status400BadRequest) : Exception(message)
{
    public int Status { get; } = status;
}

public class NotFoundException(string message) : BusinessException(message, StatusCodes.Status404NotFound);

public interface ICurrentUser
{
    int Id { get; }
    string Rol { get; }
    int? IdServicio { get; }
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public int Id => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new BusinessException("Sesión inválida", StatusCodes.Status401Unauthorized);

    public string Rol => User.FindFirstValue(ClaimTypes.Role) ?? "";

    public int? IdServicio => int.TryParse(User.FindFirstValue("id_servicio"), out var s) ? s : null;
}

public class ParametroService(NodusDbContext db)
{
    public async Task<string> GetAsync(string clave, CancellationToken ct = default)
    {
        var p = await db.Parametros.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave, ct);
        return p?.Valor ?? throw new InvalidOperationException($"Falta el parámetro de sistema '{clave}'");
    }

    /// <summary>Valor del parámetro o <paramref name="porDefecto"/> si no existe.</summary>
    public async Task<string> GetOrDefaultAsync(string clave, string porDefecto, CancellationToken ct = default) =>
        (await db.Parametros.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == clave, ct))?.Valor ?? porDefecto;

    public async Task<int> GetIntOrDefaultAsync(string clave, int porDefecto, CancellationToken ct = default) =>
        int.TryParse(await GetOrDefaultAsync(clave, porDefecto.ToString(), ct), out var n) ? n : porDefecto;

    public async Task<int> GetIntAsync(string clave, CancellationToken ct = default)
    {
        var v = await GetAsync(clave, ct);
        return int.TryParse(v, out var n)
            ? n : throw new InvalidOperationException($"El parámetro '{clave}' no es numérico: '{v}'");
    }
}

public class JwtOptions
{
    public string Issuer { get; set; } = "nodus";
    public string Audience { get; set; } = "nodus-web";
    public string Key { get; set; } = null!;
    public int ExpirationHours { get; set; } = 10;
}

public class TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> opt)
{
    public (string token, DateTime expira) Create(Usuario u)
    {
        var o = opt.Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.IdUsuario.ToString()),
            new(ClaimTypes.Name, u.NombreUsuario),
            new(ClaimTypes.Role, u.Rol.NombreRol),
            new("nombre_completo", u.NombreCompleto),
        };
        if (u.IdServicio is int s) claims.Add(new Claim("id_servicio", s.ToString()));

        var expira = DateTime.UtcNow.AddHours(o.ExpirationHours);
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: expira, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
{
    private static readonly Dictionary<string, (int status, string titulo)> Mapa = new()
    {
        ["NS000"] = (400, "Servicio o familia inexistente"),
        ["NS001"] = (409, "Equipo dado de baja patrimonial"),
        ["NS002"] = (409, "Código patrimonial inmutable"),
        ["NS003"] = (409, "Traslado bloqueado por el estado del equipo"),
        ["NS004"] = (409, "Baja patrimonial bloqueada"),
        ["NS005"] = (409, "Transición de OT inválida"),
        ["NS006"] = (409, "Consumo no editable"),
        ["NS007"] = (422, "Stock insuficiente"),
        ["NS008"] = (409, "Préstamo inválido"),
        ["NS009"] = (409, "Reporte de falla rechazado"),
        ["23505"] = (409, "Registro duplicado"),
        ["23514"] = (422, "Regla de negocio incumplida"),
        ["23503"] = (400, "Referencia inexistente"),
    };

    private static readonly Dictionary<string, string> MensajesConstraint = new()
    {
        ["ux_reporte_activo_por_equipo"] = "El equipo ya tiene un reporte de falla pendiente.",
        ["ux_ot_abierta_por_equipo"] = "El equipo ya tiene una orden de trabajo abierta.",
        ["ux_prestamo_activo_por_equipo"] = "El equipo ya está en un préstamo activo.",
        ["chk_ot_cierre"] = "La OT solo puede finalizarse con la prueba de seguridad eléctrica APROBADA.",
        ["chk_insumo_stock"] = "El stock no puede quedar negativo.",
        ["chk_ot_faltante_abierta"] = "Una OT finalizada no puede quedar marcada como bloqueada por falta de insumo.",
        ["chk_prest_finalizado"] = "Para finalizar el préstamo son obligatorios la fecha de devolución real y el estado técnico de recepción.",
        ["chk_reporte_descarte"] = "Para descartar un reporte el motivo es obligatorio.",
        ["chk_prest_equipo"] = "Datos del préstamo inconsistentes con su tipo (OTORGADO/RECIBIDO).",
        ["chk_historial_distinto"] = "El servicio de destino debe ser distinto al de origen.",
        ["equipo_nro_serie_key"] = "Ya existe un equipo con ese número de serie.",
        ["usuario_nombre_usuario_key"] = "Ya existe un usuario con ese nombre de usuario.",
        ["usuario_legajo_key"] = "Ya existe un usuario con ese legajo.",
        ["insumo_nombre_insumo_key"] = "Ya existe un insumo con ese nombre.",
        ["hospital_externo_nombre_hospital_key"] = "Ya existe un hospital con ese nombre.",
        ["baja_patrimonial_id_equipo_key"] = "El equipo ya fue dado de baja.",
    };

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            var (status, titulo, detalle, codigo) = Traducir(ex);
            if (status >= 500) log.LogError(ex, "Error no controlado");
            else log.LogInformation("Error de negocio {Codigo}: {Detalle}", codigo, detalle);

            var problem = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = detalle,
                Instance = ctx.Request.Path,
            };
            if (codigo != null) problem.Extensions["codigo"] = codigo;

            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(problem);
        }
    }

    private static (int, string, string, string?) Traducir(Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
            return (409, "Conflicto de concurrencia",
                "Otro usuario modificó este registro. Se recargaron los datos actuales; revisá y volvé a intentar.",
                "CONCURRENCIA");

        if (ex is BusinessException be)
            return (be.Status, be.Status == 404 ? "No encontrado" : "Solicitud inválida", be.Message, null);

        var pg = ex as PostgresException ?? ex.InnerException as PostgresException
                 ?? ex.InnerException?.InnerException as PostgresException;
        if (pg != null && Mapa.TryGetValue(pg.SqlState, out var m))
        {
            var detalle = pg.ConstraintName != null && MensajesConstraint.TryGetValue(pg.ConstraintName, out var txt)
                ? txt
                : pg.MessageText;
            return (m.status, m.titulo, detalle, pg.SqlState);
        }

        return (500, "Error interno", "Ocurrió un error inesperado. Contactá a Bioingeniería.", null);
    }
}

public class SeedOptions
{
    public string AdminUsuario { get; set; } = "bioingenieria";
    public string AdminPassword { get; set; } = null!;
    public string AdminNombre { get; set; } = "Bioingeniero Responsable";
    public string PasswordServicios { get; set; } = null!;
}

public static class DataSeeder
{
    private static readonly (string servicio, string login)[] UsuariosServicio =
    {
        ("UTI Adultos", "uti_adultos"),
        ("UTI Neonatal", "uti_neonatal"),
        ("Quirófano Central", "quirofano"),
        ("Guardia Central", "guardia"),
    };

    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NodusDbContext>();
        var opt = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedOptions>>().Value;
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

        var roles = await db.Roles.ToDictionaryAsync(r => r.NombreRol, r => r.IdRol);
        if (!roles.ContainsKey(Roles.Bioingeniero))
            throw new InvalidOperationException("La tabla rol está vacía: ejecutá primero database/001_nodus_schema.sql");

        if (!await db.Usuarios.AnyAsync(u => u.IdRol == roles[Roles.Bioingeniero]))
        {
            db.Usuarios.Add(new Usuario
            {
                NombreUsuario = opt.AdminUsuario,
                NombreCompleto = opt.AdminNombre,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(opt.AdminPassword),
                IdRol = roles[Roles.Bioingeniero],
                Activo = true,
            });
            log.LogWarning("Se creó el usuario Bioingeniero inicial '{U}'. Cambiá la contraseña.", opt.AdminUsuario);
        }

        var servicios = await db.Servicios.ToDictionaryAsync(s => s.NombreServicio, s => s.IdServicio);
        foreach (var (nombre, login) in UsuariosServicio)
        {
            if (!servicios.TryGetValue(nombre, out var idServ)) continue;
            if (await db.Usuarios.AnyAsync(u => u.NombreUsuario == login)) continue;
            db.Usuarios.Add(new Usuario
            {
                NombreUsuario = login,
                NombreCompleto = $"Servicio {nombre}",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(opt.PasswordServicios),
                IdRol = roles[Roles.Asistencial],
                IdServicio = idServ,
                Activo = true,
            });
            log.LogInformation("Usuario de servicio '{U}' creado", login);
        }

        await db.SaveChangesAsync();
    }
}
