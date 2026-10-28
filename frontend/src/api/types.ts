export type EstadoEquipo = 'OPERATIVO' | 'FUERA_DE_SERVICIO' | 'EN_MANTENIMIENTO' | 'EN_PRESTAMO' | 'BAJA_PATRIMONIAL';
export type EstadoReporte = 'PENDIENTE' | 'CONVERTIDO_EN_OT' | 'DESCARTADO';
export type EstadoOt = 'PENDIENTE' | 'EN_CURSO' | 'FINALIZADA';
export type TipoMantenimiento = 'CORRECTIVO' | 'PREVENTIVO' | 'TERCERIZADO';
export type ResultadoSeguridad = 'PENDIENTE' | 'APROBADA' | 'NO_APROBADA';
export type EstadoStock = 'STOCK_NORMAL' | 'STOCK_BAJO' | 'AGOTADO';
export type EstadoPrestamo = 'EN_PRESTAMO' | 'VENCIDO' | 'PRORROGA' | 'FINALIZADO';
export type TipoDireccion = 'OTORGADO' | 'RECIBIDO';
export type MotivoBaja = 'OBSOLESCENCIA' | 'IRREPARABILIDAD' | 'COSTO_REPARACION_MAYOR_60';
export type TipoAlerta = 'PREVENTIVO_PROXIMO' | 'PRESTAMO_VENCIDO' | 'TERCERIZADO_VENCIDO' | 'STOCK_BAJO' | 'REPORTE_FALLA';

export type Rol = 'Bioingeniero' | 'Técnico' | 'Administrativo' | 'Personal Asistencial';

export interface UsuarioSesion {
  id: number; nombreUsuario: string; nombreCompleto: string; rol: Rol; idServicio: number | null; nombreServicio: string | null;
}
export interface LoginResponse { token: string; expira: string; usuario: UsuarioSesion }

export interface Item { id: number; nombre: string; codigo?: string | null }
export interface Catalogos { servicios: Item[]; familias: Item[]; marcas: Item[]; tecnicos: Item[] }

export interface Equipo {
  id: number; codigoPatrimonial: string; idFamilia: number; familia: string; marca: string; modelo: string; nroSerie: string;
  idServicioActual: number; servicioActual: string; estado: EstadoEquipo;
  fechaAdquisicion: string; fechaGarantiaVencimiento: string; fechaMantenimientoPreventivo: string;
  enGarantia: boolean; preventivoProximo: boolean; version: number;
}

export interface HistorialUbicacion { fechaHora: string; origen: string; destino: string; usuario: string }
export interface OtResumen { id: number; tipo: TipoMantenimiento; estado: EstadoOt; fechaApertura: string; fechaCierre: string | null; descripcion: string | null }
export interface Baja { fecha: string; motivo: MotivoBaja; observaciones: string | null; bioingeniero: string }

export interface EquipoDetalle {
  equipo: Equipo; historial: HistorialUbicacion[]; ordenes: OtResumen[]; prestamos: Prestamo[];
  reportePendiente: ReporteFalla | null; baja: Baja | null;
}

export interface Kanban { servicios: Item[]; equipos: Equipo[] }

export interface QrEquipo {
  id: number; codigoPatrimonial: string; familia: string; marca: string; modelo: string; servicioActual: string;
  estado: EstadoEquipo; reportePendiente: boolean; puedeReportar: boolean; motivoNoReportable: string | null;
}

export interface NovedadesFallas { pendientes: number; ultimoId: number; nuevos: ReporteFalla[] }

export interface ReporteFalla {
  id: number; idEquipo: number; codigoPatrimonial: string; equipo: string; servicio: string;
  solicitante: string; matricula: string; descripcion: string; fechaHora: string;
  estado: EstadoReporte; motivoDescarte: string | null; usuarioSesion: string;
}

export interface Tercerizado {
  id: number; proveedor: string; enGarantia: boolean; fechaEnvio: string; fechaProbableVuelta: string;
  fechaVueltaReal: string | null; remitoEntrada: string | null; vencido: boolean;
}
export interface Consumo { id: number; idInsumo: number; insumo: string; cantidad: number; fecha: string; usuario: string }

export interface Ot {
  id: number; idEquipo: number; codigoPatrimonial: string; equipo: string; servicio: string; estadoEquipo: EstadoEquipo;
  idReporteFalla: number | null; descripcionFalla: string | null;
  tipo: TipoMantenimiento; estado: EstadoOt;
  fechaApertura: string; fechaInicioTaller: string | null; fechaCierre: string | null;
  descripcionTareaTecnica: string | null; seguridadElectrica: ResultadoSeguridad; observacionesSeguridadElectrica: string | null;
  idTecnicoAsignado: number | null; tecnicoAsignado: string | null; creador: string;
  tercerizado: Tercerizado | null; consumos: Consumo[];
  idInsumoFaltante: number | null; insumoFaltante: string | null; stockInsumoFaltante: number | null;
}

export interface Insumo { id: number; nombre: string; categoria: string; stockActual: number; stockMinimo: number; estadoStock: EstadoStock }
export interface MovimientoInsumo { fecha: string; tipo: 'CONSUMO' | 'REPOSICION'; cantidad: number; referencia: string; usuario: string }
export interface SolicitudCompra { id: number; idInsumo: number; insumo: string; cantidad: number; fecha: string; usuario: string }

export interface HospitalExterno { id: number; nombre: string; direccion: string | null; latitud: number; longitud: number; prestamosActivos: number }
export interface Prestamo {
  id: number; tipo: TipoDireccion; estado: EstadoPrestamo;
  idEquipo: number | null; codigoPatrimonial: string | null; descripcionEquipo: string;
  idHospitalExterno: number; hospital: string;
  responsable: string; fechaSalida: string; fechaDevolucionEstimada: string; fechaDevolucionReal: string | null;
  estadoTecnicoEntrega: string; estadoTecnicoRecepcion: string | null; accesorios: string | null; usuario: string;
}

export interface Alerta { id: number; tipo: TipoAlerta; idReferencia: number; mensaje: string; fecha: string; leida: boolean; resuelta: boolean }

export interface Usuario {
  id: number; nombreUsuario: string; legajo: string | null; nombreCompleto: string; idRol: number; rol: Rol;
  idServicio: number | null; servicio: string | null; activo: boolean; createdAt: string;
}
export interface Parametro { clave: string; valor: string }

export type Semaforo = 'OK' | 'CRITICO' | 'SIN_DATOS';
export interface DisponibilidadTipo { idFamilia: number; familia: string; total: number; operativos: number; porcentaje: number; semaforo: Semaforo }
export interface HeatmapCelda { familia: string; mes: string; fallas: number }
export interface GrillaServicio { idServicio: number; servicio: string; operativos: number; fueraDeServicio: number; enMantenimiento: number; enPrestamo: number; total: number }

export interface Kpis {
  totalEquipos: number; operativos: number; disponibilidad: number; semaforo: Semaforo;
  respiradoresLibres: number; respiradoresTotal: number; otsAbiertas: number; reportesPendientes: number; alertasAbiertas: number;
}
export interface OtBloqueada { idOt: number; codigoPatrimonial: string; equipo: string; servicio: string; motivo: 'STOCK' | 'SEGURIDAD_ELECTRICA'; detalle: string; apertura: string }
export interface TareaDia { idReferencia: number; idEquipo: number | null; titulo: string; detalle: string; fecha: string; diasRestantes: number }
export interface Conteo { nombre: string; cantidad: number }
export interface MttrFamilia { familia: string; correctivo: number | null; nCorrectivo: number; preventivo: number | null; nPreventivo: number }
export interface ParqueAnio { anio: number; vigentes: number; excedidos: number }
export interface ConsumoFamilia { familia: string; unidadesInsumos: number; serviciosExternos: number }

export interface Dashboard {
  desde: string; hasta: string; idServicio: number | null; idFamilia: number | null; umbral: number; generadoUtc: string;
  kpis: Kpis;
  porTipo: DisponibilidadTipo[];
  grilla: GrillaServicio[]; grillaTotal: GrillaServicio;
  otsBloqueadas: { porStock: number; porSeguridadElectrica: number; detalle: OtBloqueada[] };
  tareas: { preventivos: TareaDia[]; prestamos: TareaDia[]; tercerizados: TareaDia[] };
  fallasPorMarca: Conteo[];
  mttr: { globalCorrectivo: number | null; globalPreventivo: number | null; porFamilia: MttrFamilia[] };
  parque: { vidaUtilAnios: number; serie: ParqueAnio[]; totalActual: number; excedidosActual: number; porcentajeExcedido: number; edadPromedio: number };
  consumo: { porFamilia: ConsumoFamilia[]; topInsumos: Conteo[]; totalUnidades: number; totalServiciosExternos: number };
  heatmap: {
    meses: string[]; familias: string[]; porMes: HeatmapCelda[]; diaHora: number[][];
    totalFallasPeriodo: number; horaPico: number | null; diaPico: number | null;
    mesPico: string | null; mesPicoFallas: number; porcentajeInvierno: number; totalFallas12Meses: number;
  };
}
