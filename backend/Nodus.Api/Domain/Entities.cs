using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nodus.Api.Domain;

[Table("rol")]
public class Rol
{
    [Key, Column("id_rol")] public int IdRol { get; set; }
    [Column("nombre_rol")] public string NombreRol { get; set; } = null!;
}

[Table("servicio")]
public class Servicio
{
    [Key, Column("id_servicio")] public int IdServicio { get; set; }
    [Column("nombre_servicio")] public string NombreServicio { get; set; } = null!;
    [Column("codigo_servicio")] public string CodigoServicio { get; set; } = null!;
}

[Table("usuario")]
public class Usuario
{
    [Key, Column("id_usuario")] public int IdUsuario { get; set; }
    [Column("nombre_usuario")] public string NombreUsuario { get; set; } = null!;
    [Column("legajo")] public string? Legajo { get; set; }
    [Column("nombre_completo")] public string NombreCompleto { get; set; } = null!;
    [Column("password_hash")] public string PasswordHash { get; set; } = null!;
    [Column("id_rol")] public int IdRol { get; set; }
    [Column("id_servicio")] public int? IdServicio { get; set; }
    [Column("activo")] public bool Activo { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; }

    public Rol Rol { get; set; } = null!;
    public Servicio? Servicio { get; set; }
}

[Table("tipo_equipo")]
public class TipoEquipo
{
    [Key, Column("id_familia")] public int IdFamilia { get; set; }
    [Column("nombre_familia")] public string NombreFamilia { get; set; } = null!;
    [Column("codigo_familia")] public string CodigoFamilia { get; set; } = null!;
}

[Table("marca")]
public class Marca
{
    [Key, Column("id_marca")] public int IdMarca { get; set; }
    [Column("nombre_marca")] public string NombreMarca { get; set; } = null!;
}

[Table("equipo")]
public class Equipo
{
    [Key, Column("id_equipo")] public int IdEquipo { get; set; }
    [Column("codigo_patrimonial")] public string CodigoPatrimonial { get; set; } = null!;
    [Column("id_familia")] public int IdFamilia { get; set; }
    [Column("id_marca")] public int IdMarca { get; set; }
    [Column("modelo")] public string Modelo { get; set; } = null!;
    [Column("nro_serie")] public string NroSerie { get; set; } = null!;
    [Column("fecha_adquisicion")] public DateOnly FechaAdquisicion { get; set; }
    [Column("fecha_garantia_vencimiento")] public DateOnly FechaGarantiaVencimiento { get; set; }
    [Column("fecha_mantenimiento_preventivo")] public DateOnly FechaMantenimientoPreventivo { get; set; }
    [Column("id_servicio_actual")] public int IdServicioActual { get; set; }
    [Column("estado_operativo")] public EstadoEquipo EstadoOperativo { get; set; } = EstadoEquipo.OPERATIVO;
    [Column("created_at")] public DateTime CreatedAt { get; set; }

    /// <summary>Concurrencia optimista: columna de sistema xmin de PostgreSQL.</summary>
    [Timestamp] public uint Version { get; set; }

    public TipoEquipo Familia { get; set; } = null!;
    public Marca Marca { get; set; } = null!;
    public Servicio ServicioActual { get; set; } = null!;
}

[Table("contador_patrimonial")]
public class ContadorPatrimonial
{
    [Key, Column("id_familia")] public int IdFamilia { get; set; }
    [Column("ultimo")] public int Ultimo { get; set; }
}

[Table("historial_ubicacion")]
public class HistorialUbicacion
{
    [Key, Column("id_historial_ubicacion")] public int IdHistorialUbicacion { get; set; }
    [Column("id_equipo")] public int IdEquipo { get; set; }
    [Column("id_servicio_origen")] public int IdServicioOrigen { get; set; }
    [Column("id_servicio_destino")] public int IdServicioDestino { get; set; }
    [Column("fecha_hora_cambio")] public DateTime FechaHoraCambio { get; set; }
    [Column("id_usuario")] public int IdUsuario { get; set; }

    public Equipo Equipo { get; set; } = null!;
    public Servicio ServicioOrigen { get; set; } = null!;
    public Servicio ServicioDestino { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

[Table("baja_patrimonial")]
public class BajaPatrimonial
{
    [Key, Column("id_baja")] public int IdBaja { get; set; }
    [Column("id_equipo")] public int IdEquipo { get; set; }
    [Column("fecha_baja")] public DateOnly FechaBaja { get; set; }
    [Column("motivo_baja")] public MotivoBaja MotivoBaja { get; set; }
    [Column("observaciones")] public string? Observaciones { get; set; }
    [Column("id_usuario_bioingeniero")] public int IdUsuarioBioingeniero { get; set; }

    public Equipo Equipo { get; set; } = null!;
    public Usuario UsuarioBioingeniero { get; set; } = null!;
}

[Table("reporte_falla")]
public class ReporteFalla
{
    [Key, Column("id_reporte")] public int IdReporte { get; set; }
    [Column("id_equipo")] public int IdEquipo { get; set; }
    [Column("id_usuario_sesion")] public int IdUsuarioSesion { get; set; }
    [Column("nombre_solicitante")] public string NombreSolicitante { get; set; } = null!;
    [Column("apellido_solicitante")] public string ApellidoSolicitante { get; set; } = null!;
    [Column("matricula_solicitante")] public string MatriculaSolicitante { get; set; } = null!;
    [Column("descripcion_falla")] public string DescripcionFalla { get; set; } = null!;
    [Column("fecha_hora_reporte")] public DateTime FechaHoraReporte { get; set; }
    [Column("estado_reporte")] public EstadoReporte EstadoReporte { get; set; } = EstadoReporte.PENDIENTE;
    [Column("motivo_descarte")] public string? MotivoDescarte { get; set; }

    public Equipo Equipo { get; set; } = null!;
    public Usuario UsuarioSesion { get; set; } = null!;
}

[Table("orden_trabajo")]
public class OrdenTrabajo
{
    [Key, Column("id_ot")] public int IdOt { get; set; }
    [Column("id_equipo")] public int IdEquipo { get; set; }
    [Column("id_reporte_falla")] public int? IdReporteFalla { get; set; }
    [Column("tipo_mantenimiento")] public TipoMantenimiento TipoMantenimiento { get; set; }
    [Column("estado_ot")] public EstadoOt EstadoOt { get; set; } = EstadoOt.PENDIENTE;
    [Column("fecha_apertura")] public DateTime FechaApertura { get; set; }
    [Column("fecha_inicio_taller")] public DateTime? FechaInicioTaller { get; set; }
    [Column("fecha_cierre")] public DateTime? FechaCierre { get; set; }
    [Column("descripcion_tarea_tecnica")] public string? DescripcionTareaTecnica { get; set; }
    [Column("resultado_seguridad_electrica")] public ResultadoSeguridadElectrica ResultadoSeguridadElectrica { get; set; } = ResultadoSeguridadElectrica.PENDIENTE;
    [Column("observaciones_seguridad_electrica")] public string? ObservacionesSeguridadElectrica { get; set; }
    [Column("id_tecnico_asignado")] public int? IdTecnicoAsignado { get; set; }
    [Column("id_usuario_creador")] public int IdUsuarioCreador { get; set; }
    /// <summary>Insumo faltante que detiene la OT.</summary>
    [Column("id_insumo_faltante")] public int? IdInsumoFaltante { get; set; }

    public Equipo Equipo { get; set; } = null!;
    public ReporteFalla? ReporteFalla { get; set; }
    public Insumo? InsumoFaltante { get; set; }
    public Usuario? TecnicoAsignado { get; set; }
    public Usuario UsuarioCreador { get; set; } = null!;
    public DetalleMantenimientoTercerizado? Tercerizado { get; set; }
    public List<UsoInsumoOt> Consumos { get; set; } = new();
}

[Table("detalle_mantenimiento_tercerizado")]
public class DetalleMantenimientoTercerizado
{
    [Key, Column("id_tercerizado")] public int IdTercerizado { get; set; }
    [Column("id_ot")] public int IdOt { get; set; }
    [Column("proveedor_fabrica_nombre")] public string ProveedorFabricaNombre { get; set; } = null!;
    [Column("en_garantia")] public bool EnGarantia { get; set; }
    [Column("fecha_envio")] public DateOnly FechaEnvio { get; set; }
    [Column("fecha_probable_vuelta")] public DateOnly FechaProbableVuelta { get; set; }
    [Column("fecha_vuelta_real")] public DateOnly? FechaVueltaReal { get; set; }
    [Column("remito_entrada_descripcion")] public string? RemitoEntradaDescripcion { get; set; }

    public OrdenTrabajo OrdenTrabajo { get; set; } = null!;
}

[Table("insumo")]
public class Insumo
{
    [Key, Column("id_insumo")] public int IdInsumo { get; set; }
    [Column("nombre_insumo")] public string NombreInsumo { get; set; } = null!;
    [Column("categoria")] public string Categoria { get; set; } = null!;
    [Column("stock_actual")] public int StockActual { get; set; }
    [Column("stock_minimo")] public int StockMinimo { get; set; }

    /// <summary>Columna GENERATED ALWAYS ... STORED: solo lectura.</summary>
    [Column("estado_stock")] public EstadoStock EstadoStock { get; private set; }
}

[Table("uso_insumo_ot")]
public class UsoInsumoOt
{
    [Key, Column("id_uso")] public int IdUso { get; set; }
    [Column("id_ot")] public int IdOt { get; set; }
    [Column("id_insumo")] public int IdInsumo { get; set; }
    [Column("cantidad_consumida")] public int CantidadConsumida { get; set; }
    [Column("fecha_uso")] public DateTime FechaUso { get; set; }
    [Column("id_usuario")] public int IdUsuario { get; set; }

    public OrdenTrabajo OrdenTrabajo { get; set; } = null!;
    public Insumo Insumo { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

[Table("reposicion_insumo")]
public class ReposicionInsumo
{
    [Key, Column("id_reposicion")] public int IdReposicion { get; set; }
    [Column("id_insumo")] public int IdInsumo { get; set; }
    [Column("cantidad_ingresada")] public int CantidadIngresada { get; set; }
    [Column("fecha_reposicion")] public DateTime FechaReposicion { get; set; }
    [Column("id_usuario")] public int IdUsuario { get; set; }

    public Insumo Insumo { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

[Table("solicitud_compra_insumo")]
public class SolicitudCompraInsumo
{
    [Key, Column("id_solicitud")] public int IdSolicitud { get; set; }
    [Column("id_insumo")] public int IdInsumo { get; set; }
    [Column("cantidad_a_comprar")] public int CantidadAComprar { get; set; }
    [Column("fecha_solicitud")] public DateTime FechaSolicitud { get; set; }
    [Column("id_usuario")] public int IdUsuario { get; set; }

    public Insumo Insumo { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

[Table("hospital_externo")]
public class HospitalExterno
{
    [Key, Column("id_hospital_externo")] public int IdHospitalExterno { get; set; }
    [Column("nombre_hospital")] public string NombreHospital { get; set; } = null!;
    [Column("direccion")] public string? Direccion { get; set; }
    [Column("latitud")] public decimal Latitud { get; set; }
    [Column("longitud")] public decimal Longitud { get; set; }
}

[Table("prestamo_equipo")]
public class PrestamoEquipo
{
    [Key, Column("id_prestamo")] public int IdPrestamo { get; set; }
    [Column("tipo_direccion")] public TipoDireccionPrestamo TipoDireccion { get; set; }
    [Column("id_equipo")] public int? IdEquipo { get; set; }
    [Column("marca_externa")] public string? MarcaExterna { get; set; }
    [Column("modelo_externo")] public string? ModeloExterno { get; set; }
    [Column("nro_serie_externo")] public string? NroSerieExterno { get; set; }
    [Column("id_hospital_externo")] public int IdHospitalExterno { get; set; }
    [Column("responsable_retiro_entrega")] public string ResponsableRetiroEntrega { get; set; } = null!;
    [Column("fecha_salida")] public DateOnly FechaSalida { get; set; }
    [Column("fecha_devolucion_estimada")] public DateOnly FechaDevolucionEstimada { get; set; }
    [Column("fecha_devolucion_real")] public DateOnly? FechaDevolucionReal { get; set; }
    [Column("estado_prestamo")] public EstadoPrestamo EstadoPrestamo { get; set; } = EstadoPrestamo.EN_PRESTAMO;
    [Column("estado_tecnico_entrega")] public string EstadoTecnicoEntrega { get; set; } = null!;
    [Column("estado_tecnico_recepcion")] public string? EstadoTecnicoRecepcion { get; set; }
    [Column("accesorios_prestados")] public string? AccesoriosPrestados { get; set; }
    [Column("id_usuario")] public int IdUsuario { get; set; }

    public Equipo? Equipo { get; set; }
    public HospitalExterno HospitalExterno { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

[Table("parametro_sistema")]
public class ParametroSistema
{
    [Key, Column("clave")] public string Clave { get; set; } = null!;
    [Column("valor")] public string Valor { get; set; } = null!;
}

[Table("alerta")]
public class Alerta
{
    [Key, Column("id_alerta")] public int IdAlerta { get; set; }
    [Column("tipo_alerta")] public TipoAlerta TipoAlerta { get; set; }
    [Column("id_referencia")] public int IdReferencia { get; set; }
    [Column("mensaje")] public string Mensaje { get; set; } = null!;
    [Column("fecha_generacion")] public DateTime FechaGeneracion { get; set; }
    [Column("leida")] public bool Leida { get; set; }
    [Column("resuelta")] public bool Resuelta { get; set; }
}
