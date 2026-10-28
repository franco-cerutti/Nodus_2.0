using System.ComponentModel.DataAnnotations;
using Nodus.Api.Domain;

namespace Nodus.Api.Dtos;

public record LoginRequest([Required] string NombreUsuario, [Required] string Password);
public record UsuarioSesionDto(int Id, string NombreUsuario, string NombreCompleto, string Rol, int? IdServicio, string? NombreServicio);
public record LoginResponse(string Token, DateTime Expira, UsuarioSesionDto Usuario);

public record ItemDto(int Id, string Nombre, string? Codigo = null);
public record CatalogosDto(List<ItemDto> Servicios, List<ItemDto> Familias, List<ItemDto> Marcas, List<ItemDto> Tecnicos);
public record CrearMarcaRequest([Required, MaxLength(100)] string Nombre);

public record EquipoListDto(
    int Id, string CodigoPatrimonial, int IdFamilia, string Familia, string Marca, string Modelo, string NroSerie,
    int IdServicioActual, string ServicioActual, EstadoEquipo Estado,
    DateOnly FechaAdquisicion, DateOnly FechaGarantiaVencimiento, DateOnly FechaMantenimientoPreventivo,
    bool EnGarantia, bool PreventivoProximo, uint Version);

public record CrearEquipoRequest(
    [Range(1, int.MaxValue)] int IdServicio,
    [Range(1, int.MaxValue)] int IdFamilia,
    [Range(1, int.MaxValue)] int IdMarca,
    [Required, MaxLength(80)] string Modelo,
    [Required, MaxLength(80)] string NroSerie,
    DateOnly FechaAdquisicion);

public record HistorialUbicacionDto(DateTime FechaHora, string Origen, string Destino, string Usuario);
public record OtResumenDto(int Id, TipoMantenimiento Tipo, EstadoOt Estado, DateTime FechaApertura, DateTime? FechaCierre, string? Descripcion);
public record BajaDto(DateOnly Fecha, MotivoBaja Motivo, string? Observaciones, string Bioingeniero);

public record EquipoDetalleDto(
    EquipoListDto Equipo,
    List<HistorialUbicacionDto> Historial,
    List<OtResumenDto> Ordenes,
    List<PrestamoDto> Prestamos,
    ReporteFallaDto? ReportePendiente,
    BajaDto? Baja);

public record TrasladoRequest([Range(1, int.MaxValue)] int IdServicioDestino, uint Version);

public record BajaRequest(
    MotivoBaja Motivo,
    [MaxLength(2000)] string? Observaciones,
    [Required] string ConfirmacionCodigo);

public record KanbanDto(List<ItemDto> Servicios, List<EquipoListDto> Equipos);

public record QrEquipoDto(
    int Id, string CodigoPatrimonial, string Familia, string Marca, string Modelo,
    string ServicioActual, EstadoEquipo Estado, bool ReportePendiente, bool PuedeReportar, string? MotivoNoReportable);

public record CrearReporteRequest(
    [Required, MaxLength(80)] string Nombre,
    [Required, MaxLength(80)] string Apellido,
    [Required, MaxLength(30)] string Matricula,
    [Required, MinLength(5), MaxLength(2000)] string DescripcionFalla);

public record NovedadesFallasDto(int Pendientes, int UltimoId, List<ReporteFallaDto> Nuevos);

public record ReporteFallaDto(
    int Id, int IdEquipo, string CodigoPatrimonial, string Equipo, string Servicio,
    string Solicitante, string Matricula, string Descripcion, DateTime FechaHora,
    EstadoReporte Estado, string? MotivoDescarte, string UsuarioSesion);

public record ConvertirReporteRequest(int? IdTecnicoAsignado);
public record DescartarReporteRequest([Required, MinLength(3)] string Motivo);

public record CrearOtRequest(
    [Range(1, int.MaxValue)] int IdEquipo,
    TipoMantenimiento Tipo,
    int? IdTecnicoAsignado,
    string? DescripcionTareaTecnica,
    TercerizadoRequest? Tercerizado);

public record TercerizadoRequest(
    [Required, MaxLength(150)] string ProveedorFabricaNombre,
    bool EnGarantia,
    DateOnly FechaEnvio);

public record ActualizarOtRequest(string? DescripcionTareaTecnica, int? IdTecnicoAsignado);
public record IniciarOtRequest(int? IdTecnicoAsignado);
public record SeguridadElectricaRequest(ResultadoSeguridadElectrica Resultado, [MaxLength(2000)] string? Observaciones);
public record FinalizarOtRequest(string? DescripcionTareaTecnica);
public record RetornoTercerizadoRequest(DateOnly FechaVueltaReal, string? RemitoEntradaDescripcion);

public record TercerizadoDto(int Id, string Proveedor, bool EnGarantia, DateOnly FechaEnvio,
    DateOnly FechaProbableVuelta, DateOnly? FechaVueltaReal, string? RemitoEntrada, bool Vencido);

public record ConsumoDto(int Id, int IdInsumo, string Insumo, int Cantidad, DateTime Fecha, string Usuario);
public record CrearConsumoRequest([Range(1, int.MaxValue)] int IdInsumo, [Range(1, int.MaxValue)] int Cantidad);

public record OtDto(
    int Id, int IdEquipo, string CodigoPatrimonial, string Equipo, string Servicio, EstadoEquipo EstadoEquipo,
    int? IdReporteFalla, string? DescripcionFalla,
    TipoMantenimiento Tipo, EstadoOt Estado,
    DateTime FechaApertura, DateTime? FechaInicioTaller, DateTime? FechaCierre,
    string? DescripcionTareaTecnica, ResultadoSeguridadElectrica SeguridadElectrica, string? ObservacionesSeguridadElectrica,
    int? IdTecnicoAsignado, string? TecnicoAsignado, string Creador,
    TercerizadoDto? Tercerizado, List<ConsumoDto> Consumos,
    int? IdInsumoFaltante, string? InsumoFaltante, int? StockInsumoFaltante);

public record EsperaInsumoRequest(int? IdInsumo);

public record InsumoDto(int Id, string Nombre, string Categoria, int StockActual, int StockMinimo, EstadoStock EstadoStock);

public record CrearInsumoRequest(
    [Required, MaxLength(100)] string Nombre,
    [Required, MaxLength(50)] string Categoria,
    [Range(0, int.MaxValue)] int StockInicial,
    [Range(0, int.MaxValue)] int StockMinimo);

public record ActualizarInsumoRequest(
    [Required, MaxLength(100)] string Nombre,
    [Required, MaxLength(50)] string Categoria,
    [Range(0, int.MaxValue)] int StockMinimo);

public record CantidadRequest([Range(1, int.MaxValue)] int Cantidad);
public record MovimientoInsumoDto(DateTime Fecha, string Tipo, int Cantidad, string Referencia, string Usuario);
public record SolicitudCompraDto(int Id, int IdInsumo, string Insumo, int Cantidad, DateTime Fecha, string Usuario);

public record HospitalExternoDto(int Id, string Nombre, string? Direccion, decimal Latitud, decimal Longitud, int PrestamosActivos);

public record HospitalExternoRequest(
    [Required, MaxLength(150)] string Nombre,
    [MaxLength(200)] string? Direccion,
    [Range(-90, 90)] decimal Latitud,
    [Range(-180, 180)] decimal Longitud);

public record PrestamoDto(
    int Id, TipoDireccionPrestamo Tipo, EstadoPrestamo Estado,
    int? IdEquipo, string? CodigoPatrimonial, string DescripcionEquipo,
    int IdHospitalExterno, string Hospital,
    string Responsable, DateOnly FechaSalida, DateOnly FechaDevolucionEstimada, DateOnly? FechaDevolucionReal,
    string EstadoTecnicoEntrega, string? EstadoTecnicoRecepcion, string? Accesorios, string Usuario);

public record CrearPrestamoOtorgadoRequest(
    [Range(1, int.MaxValue)] int IdEquipo,
    [Range(1, int.MaxValue)] int IdHospitalExterno,
    [Required, MaxLength(100)] string Responsable,
    DateOnly FechaSalida,
    DateOnly FechaDevolucionEstimada,
    [Required] string EstadoTecnicoEntrega,
    string? Accesorios);

public record CrearPrestamoRecibidoRequest(
    [Required, MaxLength(100)] string MarcaExterna,
    [Required, MaxLength(80)] string ModeloExterno,
    [MaxLength(80)] string? NroSerieExterno,
    [Range(1, int.MaxValue)] int IdHospitalExterno,
    [Required, MaxLength(100)] string Responsable,
    DateOnly FechaSalida,
    DateOnly FechaDevolucionEstimada,
    [Required] string EstadoTecnicoEntrega,
    string? Accesorios);

public record ProrrogaRequest(DateOnly NuevaFechaDevolucion);
public record FinalizarPrestamoRequest(DateOnly FechaDevolucionReal, [Required] string EstadoTecnicoRecepcion);

public record AlertaDto(int Id, TipoAlerta Tipo, int IdReferencia, string Mensaje, DateTime Fecha, bool Leida, bool Resuelta);

public record UsuarioDto(int Id, string NombreUsuario, string? Legajo, string NombreCompleto, int IdRol, string Rol,
    int? IdServicio, string? Servicio, bool Activo, DateTime CreatedAt);

public record CrearUsuarioRequest(
    [Required, MaxLength(50)] string NombreUsuario,
    [MaxLength(20)] string? Legajo,
    [Required, MaxLength(100)] string NombreCompleto,
    [Required, MinLength(8)] string Password,
    [Range(1, int.MaxValue)] int IdRol,
    int? IdServicio);

public record ActualizarUsuarioRequest(
    [MaxLength(20)] string? Legajo,
    [Required, MaxLength(100)] string NombreCompleto,
    [Range(1, int.MaxValue)] int IdRol,
    int? IdServicio,
    bool Activo,
    [MinLength(8)] string? NuevaPassword);

public record ParametroDto(string Clave, string Valor);
public record ActualizarParametroRequest([Required, MaxLength(100)] string Valor);

public record DisponibilidadTipoDto(int IdFamilia, string Familia, int Total, int Operativos, decimal Porcentaje, string Semaforo);
public record HeatmapCeldaDto(string Familia, string Mes, int Fallas);
public record GrillaServicioDto(int IdServicio, string Servicio, int Operativos, int FueraDeServicio, int EnMantenimiento, int EnPrestamo, int Total);

public record KpisDto(
    int TotalEquipos, int Operativos, decimal Disponibilidad, string Semaforo,
    int RespiradoresLibres, int RespiradoresTotal,
    int OtsAbiertas, int ReportesPendientes, int AlertasAbiertas);

public record OtBloqueadaDto(int IdOt, string CodigoPatrimonial, string Equipo, string Servicio,
    string Motivo, string Detalle, DateTime Apertura);
public record OtsBloqueadasDto(int PorStock, int PorSeguridadElectrica, List<OtBloqueadaDto> Detalle);

public record TareaDiaDto(int IdReferencia, int? IdEquipo, string Titulo, string Detalle, DateOnly Fecha, int DiasRestantes);
public record TareasDiaDto(List<TareaDiaDto> Preventivos, List<TareaDiaDto> Prestamos, List<TareaDiaDto> Tercerizados);

public record ConteoDto(string Nombre, int Cantidad);

/// <summary>MTTR en días, desde el reporte (o la apertura) hasta el cierre, por tipo de equipo.</summary>
public record MttrFamiliaDto(string Familia, decimal? Correctivo, int NCorrectivo, decimal? Preventivo, int NPreventivo);
public record MttrDto(decimal? GlobalCorrectivo, decimal? GlobalPreventivo, List<MttrFamiliaDto> PorFamilia);

public record ParqueAnioDto(int Anio, int Vigentes, int Excedidos);
public record ParqueDto(int VidaUtilAnios, List<ParqueAnioDto> Serie, int TotalActual, int ExcedidosActual,
    decimal PorcentajeExcedido, decimal EdadPromedio);

public record ConsumoFamiliaDto(string Familia, int UnidadesInsumos, int ServiciosExternos);
public record ConsumoRecursosDto(List<ConsumoFamiliaDto> PorFamilia, List<ConteoDto> TopInsumos, int TotalUnidades, int TotalServiciosExternos);

public record HeatmapDto(
    List<string> Meses, List<string> Familias, List<HeatmapCeldaDto> PorMes,
    int[][] DiaHora,
    int TotalFallasPeriodo, int? HoraPico, int? DiaPico,
    string? MesPico, int MesPicoFallas, decimal PorcentajeInvierno, int TotalFallas12Meses);

public record DashboardDto(
    DateOnly Desde, DateOnly Hasta, int? IdServicio, int? IdFamilia, decimal Umbral, DateTime GeneradoUtc,
    KpisDto Kpis,
    List<DisponibilidadTipoDto> PorTipo,
    List<GrillaServicioDto> Grilla, GrillaServicioDto GrillaTotal,
    OtsBloqueadasDto OtsBloqueadas,
    TareasDiaDto Tareas,
    List<ConteoDto> FallasPorMarca,
    MttrDto Mttr,
    ParqueDto Parque,
    ConsumoRecursosDto Consumo,
    HeatmapDto Heatmap);
