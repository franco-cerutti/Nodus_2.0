using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nodus.Api.Documentos;
using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;
using Nodus.Api.Services;

namespace Nodus.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AuthService auth, ICurrentUser user) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous]
    public Task<LoginResponse> Login(LoginRequest r, CancellationToken ct) => auth.LoginAsync(r, ct);

    [HttpGet("me"), Authorize]
    public Task<UsuarioSesionDto> Me(CancellationToken ct) => auth.MeAsync(user.Id, ct);
}

[ApiController, Route("api/catalogos"), Authorize(Roles = Roles.Sector)]
public class CatalogosController(CatalogoService catalogos) : ControllerBase
{
    [HttpGet]
    public Task<CatalogosDto> Get(CancellationToken ct) => catalogos.ObtenerAsync(ct);

    [HttpPost("marcas"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<ItemDto> CrearMarca(CrearMarcaRequest r, CancellationToken ct) => catalogos.CrearMarcaAsync(r, ct);
}

[ApiController, Route("api/equipos"), Authorize(Roles = Roles.Sector)]
public class EquiposController(EquipoService equipos, PrestamoService prestamos, MantenimientoService mant) : ControllerBase
{
    [HttpGet]
    public Task<List<EquipoListDto>> Listar([FromQuery] int? servicio, [FromQuery] EstadoEquipo? estado,
        [FromQuery] int? familia, [FromQuery] string? q, [FromQuery] bool incluirBajas, CancellationToken ct) =>
        equipos.ListarAsync(servicio, estado, familia, q, incluirBajas, ct);

    [HttpGet("kanban")]
    public Task<KanbanDto> Kanban(CancellationToken ct) => equipos.KanbanAsync(ct);

    [HttpGet("{id:int}")]
    public Task<EquipoDetalleDto> Detalle(int id, CancellationToken ct) => equipos.DetalleAsync(id, prestamos, mant, ct);

    [HttpPost, Authorize(Roles = Roles.Bioingeniero)]
    public async Task<ActionResult<EquipoListDto>> Crear(CrearEquipoRequest r, CancellationToken ct)
    {
        var e = await equipos.CrearAsync(r, ct);
        return CreatedAtAction(nameof(Detalle), new { id = e.Id }, e);
    }

    /// <summary>Traslado entre servicios (Kanban). 409 si la versión (xmin) no coincide.</summary>
    [HttpPut("{id:int}/ubicacion"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<EquipoListDto> Trasladar(int id, TrasladoRequest r, CancellationToken ct) => equipos.TrasladarAsync(id, r, ct);

    [HttpPost("{id:int}/baja"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<EquipoListDto> Baja(int id, BajaRequest r, CancellationToken ct) => equipos.DarDeBajaAsync(id, r, ct);
}

[ApiController, Route("api/qr"), Authorize]
public class QrController(EquipoService equipos, MantenimientoService mant) : ControllerBase
{
    [HttpGet("{codigo}")]
    public Task<QrEquipoDto> Ver(string codigo, CancellationToken ct) => equipos.QrAsync(codigo, ct);

    [HttpPost("{codigo}/reportes"), Authorize(Roles = Roles.Asistencial + "," + Roles.Mantenimiento)]
    public Task<ReporteFallaDto> Reportar(string codigo, CrearReporteRequest r, CancellationToken ct) =>
        equipos.ReportarFallaAsync(codigo, r, mant, ct);
}

[ApiController, Route("api/reportes-falla"), Authorize(Roles = Roles.Mantenimiento)]
public class ReportesFallaController(MantenimientoService mant) : ControllerBase
{
    [HttpGet]
    public Task<List<ReporteFallaDto>> Listar([FromQuery] EstadoReporte? estado, [FromQuery] int? idEquipo, CancellationToken ct) =>
        mant.ListarReportesAsync(estado, idEquipo, ct);

    [HttpGet("novedades")]
    public Task<NovedadesFallasDto> Novedades([FromQuery] int? despuesDe, CancellationToken ct) => mant.NovedadesAsync(despuesDe, ct);

    [HttpPost("{id:int}/convertir")]
    public Task<OtDto> Convertir(int id, ConvertirReporteRequest r, CancellationToken ct) => mant.ConvertirReporteAsync(id, r, ct);

    [HttpPost("{id:int}/descartar")]
    public Task<ReporteFallaDto> Descartar(int id, DescartarReporteRequest r, CancellationToken ct) => mant.DescartarReporteAsync(id, r, ct);
}

[ApiController, Route("api/ots"), Authorize(Roles = Roles.Mantenimiento)]
public class OrdenesTrabajoController(MantenimientoService mant, EquipoService equipos, PdfService pdf) : ControllerBase
{
    [HttpGet]
    public Task<List<OtDto>> Listar([FromQuery] EstadoOt? estado, [FromQuery] TipoMantenimiento? tipo,
        [FromQuery] int? idEquipo, [FromQuery] bool soloAbiertas, CancellationToken ct) =>
        mant.ListarOtsAsync(estado, tipo, idEquipo, soloAbiertas, ct);

    [HttpGet("{id:int}")]
    public Task<OtDto> Obtener(int id, CancellationToken ct) => mant.ObtenerOtAsync(id, ct);

    [HttpPost]
    public Task<OtDto> Crear(CrearOtRequest r, CancellationToken ct) => mant.CrearOtAsync(r, ct);

    [HttpPut("{id:int}")]
    public Task<OtDto> Actualizar(int id, ActualizarOtRequest r, CancellationToken ct) => mant.ActualizarOtAsync(id, r, ct);

    [HttpPut("{id:int}/iniciar")]
    public Task<OtDto> Iniciar(int id, IniciarOtRequest r, CancellationToken ct) => mant.IniciarOtAsync(id, r, ct);

    [HttpPut("{id:int}/seguridad-electrica")]
    public Task<OtDto> SeguridadElectrica(int id, SeguridadElectricaRequest r, CancellationToken ct) =>
        mant.RegistrarSeguridadElectricaAsync(id, r, ct);

    [HttpPut("{id:int}/finalizar")]
    public Task<OtDto> Finalizar(int id, FinalizarOtRequest r, CancellationToken ct) => mant.FinalizarOtAsync(id, r, ct);

    [HttpPost("{id:int}/tercerizar")]
    public Task<OtDto> Tercerizar(int id, TercerizadoRequest r, CancellationToken ct) => mant.TercerizarAsync(id, r, ct);

    [HttpPut("{id:int}/tercerizado/retorno")]
    public Task<OtDto> Retorno(int id, RetornoTercerizadoRequest r, CancellationToken ct) => mant.RegistrarRetornoAsync(id, r, ct);

    [HttpPost("{id:int}/consumos")]
    public Task<OtDto> AgregarConsumo(int id, CrearConsumoRequest r, CancellationToken ct) => mant.AgregarConsumoAsync(id, r, ct);

    [HttpPut("{id:int}/espera-insumo")]
    public Task<OtDto> EsperaInsumo(int id, EsperaInsumoRequest r, CancellationToken ct) => mant.MarcarEsperaInsumoAsync(id, r.IdInsumo, ct);

    [HttpDelete("{id:int}/consumos/{idUso:int}")]
    public Task<OtDto> EliminarConsumo(int id, int idUso, CancellationToken ct) => mant.EliminarConsumoAsync(id, idUso, ct);

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct) =>
        File(pdf.OrdenTrabajo(await mant.ObtenerOtAsync(id, ct)), "application/pdf", $"OT-{id}.pdf");

    [HttpGet("{id:int}/remito-tercerizado")]
    public async Task<IActionResult> RemitoTercerizado(int id, CancellationToken ct)
    {
        var ot = await mant.ObtenerOtAsync(id, ct);
        var eq = await equipos.ObtenerAsync(ot.IdEquipo, ct);
        return File(pdf.RemitoTercerizado(ot, eq), "application/pdf", $"Remito-tercerizado-OT-{id}.pdf");
    }
}

[ApiController, Route("api/insumos")]
public class InsumosController(InsumoService insumos, PdfService pdf) : ControllerBase
{
    [HttpGet, Authorize(Roles = Roles.InsumosLectura)]
    public Task<List<InsumoDto>> Listar([FromQuery] EstadoStock? estado, CancellationToken ct) => insumos.ListarAsync(estado, ct);

    [HttpGet("{id:int}"), Authorize(Roles = Roles.InsumosLectura)]
    public Task<InsumoDto> Obtener(int id, CancellationToken ct) => insumos.ObtenerAsync(id, ct);

    [HttpGet("{id:int}/movimientos"), Authorize(Roles = Roles.InsumosLectura)]
    public Task<List<MovimientoInsumoDto>> Movimientos(int id, CancellationToken ct) => insumos.MovimientosAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Insumos)]
    public Task<InsumoDto> Crear(CrearInsumoRequest r, CancellationToken ct) => insumos.CrearAsync(r, ct);

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Insumos)]
    public Task<InsumoDto> Actualizar(int id, ActualizarInsumoRequest r, CancellationToken ct) => insumos.ActualizarAsync(id, r, ct);

    [HttpPost("{id:int}/reposiciones"), Authorize(Roles = Roles.Insumos)]
    public Task<InsumoDto> Reponer(int id, CantidadRequest r, CancellationToken ct) => insumos.ReponerAsync(id, r.Cantidad, ct);

    [HttpPost("{id:int}/solicitudes-compra"), Authorize(Roles = Roles.Insumos)]
    public Task<SolicitudCompraDto> SolicitarCompra(int id, CantidadRequest r, CancellationToken ct) =>
        insumos.SolicitarCompraAsync(id, r.Cantidad, ct);

    [HttpGet("solicitudes-compra"), Authorize(Roles = Roles.Insumos)]
    public Task<List<SolicitudCompraDto>> Solicitudes(CancellationToken ct) => insumos.ListarSolicitudesAsync(ct);

    [HttpGet("solicitudes-compra/{id:int}/pdf"), Authorize(Roles = Roles.Insumos)]
    public async Task<IActionResult> NotaCompra(int id, CancellationToken ct)
    {
        var s = await insumos.ObtenerSolicitudAsync(id, ct);
        var i = await insumos.ObtenerAsync(s.IdInsumo, ct);
        return File(pdf.NotaCompra(s, i), "application/pdf", $"Nota-de-compra-{id}.pdf");
    }
}

[ApiController, Route("api/prestamos"), Authorize(Roles = Roles.Sector)]
public class PrestamosController(PrestamoService prestamos, PdfService pdf) : ControllerBase
{
    [HttpGet]
    public Task<List<PrestamoDto>> Listar([FromQuery] EstadoPrestamo? estado, [FromQuery] TipoDireccionPrestamo? tipo, CancellationToken ct) =>
        prestamos.ListarAsync(estado, tipo, null, ct);

    [HttpGet("{id:int}")]
    public Task<PrestamoDto> Obtener(int id, CancellationToken ct) => prestamos.ObtenerAsync(id, ct);

    [HttpPost("otorgados"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<PrestamoDto> Otorgar(CrearPrestamoOtorgadoRequest r, CancellationToken ct) => prestamos.OtorgarAsync(r, ct);

    [HttpPost("recibidos"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<PrestamoDto> Recibir(CrearPrestamoRecibidoRequest r, CancellationToken ct) => prestamos.RecibirAsync(r, ct);

    [HttpPut("{id:int}/prorroga"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<PrestamoDto> Prorrogar(int id, ProrrogaRequest r, CancellationToken ct) => prestamos.ProrrogarAsync(id, r, ct);

    [HttpPut("{id:int}/finalizar"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<PrestamoDto> Finalizar(int id, FinalizarPrestamoRequest r, CancellationToken ct) => prestamos.FinalizarAsync(id, r, ct);

    [HttpGet("{id:int}/remito")]
    public async Task<IActionResult> Remito(int id, CancellationToken ct) =>
        File(pdf.RemitoPrestamo(await prestamos.ObtenerAsync(id, ct)), "application/pdf", $"Remito-prestamo-{id}.pdf");

    [HttpGet("{id:int}/nota")]
    public async Task<IActionResult> Nota(int id, CancellationToken ct) =>
        File(pdf.NotaInstitucional(await prestamos.ObtenerAsync(id, ct)), "application/pdf", $"Nota-prestamo-{id}.pdf");
}

[ApiController, Route("api/hospitales-externos"), Authorize(Roles = Roles.Sector)]
public class HospitalesExternosController(PrestamoService prestamos) : ControllerBase
{
    [HttpGet]
    public Task<List<HospitalExternoDto>> Listar(CancellationToken ct) => prestamos.ListarHospitalesAsync(ct);

    [HttpPost, Authorize(Roles = Roles.Bioingeniero)]
    public Task<HospitalExternoDto> Crear(HospitalExternoRequest r, CancellationToken ct) => prestamos.GuardarHospitalAsync(null, r, ct);

    [HttpPut("{id:int}"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<HospitalExternoDto> Actualizar(int id, HospitalExternoRequest r, CancellationToken ct) => prestamos.GuardarHospitalAsync(id, r, ct);
}

[ApiController, Route("api/reportes"), Authorize(Roles = Roles.Sector)]
public class ReportesController(ReporteService reportes, DashboardService dashboard, PdfService pdf, ExcelService excel) : ControllerBase
{
    /// <summary>Tablero de control filtrado por período (desde/hasta), servicio y familia.</summary>
    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] int? servicio, [FromQuery] int? familia, CancellationToken ct) =>
        dashboard.ObtenerAsync(desde, hasta, servicio, familia, ct);

    [HttpGet("dashboard/excel")]
    public async Task<IActionResult> DashboardExcel([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta,
        [FromQuery] int? servicio, [FromQuery] int? familia, CancellationToken ct)
    {
        var d = await dashboard.ObtenerAsync(desde, hasta, servicio, familia, ct);
        var filtros = await dashboard.DescribirFiltrosAsync(d, ct);
        var quien = User.FindFirstValue("nombre_completo") ?? User.Identity?.Name ?? "NODUS";
        return File(excel.Tablero(d, filtros, quien),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Tablero-NODUS-{d.Desde:yyyyMMdd}-{d.Hasta:yyyyMMdd}.xlsx");
    }

    /// <summary>Consolidado General Mensual. formato = pdf | xlsx</summary>
    [HttpGet("consolidado")]
    public async Task<IActionResult> Consolidado([FromQuery] int anio, [FromQuery] int mes, [FromQuery] string formato = "pdf",
        CancellationToken ct = default)
    {
        var quien = User.FindFirstValue("nombre_completo") ?? User.Identity?.Name ?? "NODUS";
        var data = await reportes.ConsolidadoAsync(anio, mes, quien, ct);
        var nombre = $"Consolidado-NODUS-{anio}-{mes:D2}";
        return formato.ToLowerInvariant() switch
        {
            "xlsx" or "excel" => File(excel.Consolidado(data),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre + ".xlsx"),
            "pdf" => File(pdf.Consolidado(data), "application/pdf", nombre + ".pdf"),
            _ => throw new BusinessException("Formato no soportado: usá 'pdf' o 'xlsx'."),
        };
    }
}

[ApiController, Route("api/alertas"), Authorize(Roles = Roles.Sector)]
public class AlertasController(AlertaService alertas) : ControllerBase
{
    [HttpGet]
    public Task<List<AlertaDto>> Listar([FromQuery] bool soloAbiertas = true, CancellationToken ct = default) =>
        alertas.ListarAsync(soloAbiertas, ct);

    [HttpPut("{id:int}/leida")]
    public async Task<IActionResult> Leida(int id, CancellationToken ct)
    {
        await alertas.MarcarLeidaAsync(id, ct);
        return NoContent();
    }
}

[ApiController, Route("api/admin"), Authorize(Roles = Roles.Admin)]
public class AdminController(AdminService admin) : ControllerBase
{
    [HttpGet("usuarios")]
    public Task<List<UsuarioDto>> Usuarios(CancellationToken ct) => admin.ListarUsuariosAsync(ct);

    [HttpGet("roles")]
    public Task<List<ItemDto>> RolesList(CancellationToken ct) => admin.RolesAsync(ct);

    [HttpPost("usuarios"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<UsuarioDto> CrearUsuario(CrearUsuarioRequest r, CancellationToken ct) => admin.CrearUsuarioAsync(r, ct);

    [HttpPut("usuarios/{id:int}")]
    public Task<UsuarioDto> ActualizarUsuario(int id, ActualizarUsuarioRequest r, CancellationToken ct) =>
        admin.ActualizarUsuarioAsync(id, r, ct);

    [HttpGet("parametros")]
    public Task<List<ParametroDto>> ParametrosList(CancellationToken ct) => admin.ParametrosAsync(ct);

    [HttpPut("parametros/{clave}"), Authorize(Roles = Roles.Bioingeniero)]
    public Task<ParametroDto> ActualizarParametro(string clave, ActualizarParametroRequest r, CancellationToken ct) =>
        admin.ActualizarParametroAsync(clave, r.Valor, ct);
}
