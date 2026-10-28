namespace Nodus.Api.Domain;

public enum EstadoEquipo { OPERATIVO, FUERA_DE_SERVICIO, EN_MANTENIMIENTO, EN_PRESTAMO, BAJA_PATRIMONIAL }

public enum EstadoReporte { PENDIENTE, CONVERTIDO_EN_OT, DESCARTADO }

public enum EstadoOt { PENDIENTE, EN_CURSO, FINALIZADA }

public enum TipoMantenimiento { CORRECTIVO, PREVENTIVO, TERCERIZADO }

public enum ResultadoSeguridadElectrica { PENDIENTE, APROBADA, NO_APROBADA }

public enum EstadoStock { STOCK_NORMAL, STOCK_BAJO, AGOTADO }

public enum EstadoPrestamo { EN_PRESTAMO, VENCIDO, PRORROGA, FINALIZADO }

public enum TipoDireccionPrestamo { OTORGADO, RECIBIDO }

public enum MotivoBaja { OBSOLESCENCIA, IRREPARABILIDAD, COSTO_REPARACION_MAYOR_60 }

public enum TipoAlerta { PREVENTIVO_PROXIMO, PRESTAMO_VENCIDO, TERCERIZADO_VENCIDO, STOCK_BAJO, REPORTE_FALLA }

/// <summary>Sigla patrimonial de cada servicio clínico. Mapeo explícito y cerrado.</summary>
public static class SiglasServicio
{
    public static readonly IReadOnlyDictionary<string, string> PorNombre = new Dictionary<string, string>
    {
        ["UTI Adultos"] = "UTIA",
        ["UTI Neonatal"] = "UTIN",
        ["Quirófano Central"] = "QUIR",
        ["Guardia Central"] = "GUARD",
    };

    public static string Obtener(string nombreServicio) =>
        PorNombre.TryGetValue(nombreServicio, out var sigla)
            ? sigla
            : throw new InvalidOperationException($"El servicio '{nombreServicio}' no tiene sigla patrimonial asignada.");
}

/// <summary>Nombres exactos de la tabla rol (se usan en los claims y en [Authorize(Roles=...)]).</summary>
public static class Roles
{
    public const string Bioingeniero = "Bioingeniero";
    public const string Tecnico = "Técnico";
    public const string Administrativo = "Administrativo";
    public const string Asistencial = "Personal Asistencial";

    public const string Sector = Bioingeniero + "," + Tecnico + "," + Administrativo;
    public const string Mantenimiento = Bioingeniero + "," + Tecnico;
    public const string Insumos = Bioingeniero + "," + Administrativo;
    public const string InsumosLectura = Bioingeniero + "," + Tecnico + "," + Administrativo;
    public const string Admin = Bioingeniero + "," + Administrativo;
}

/// <summary>Claves de parametro_sistema.</summary>
public static class Parametros
{
    public const string CodigoHospital = "CODIGO_HOSPITAL";
    public const string DiasAlertaPreventivo = "DIAS_ALERTA_PREVENTIVO";
    public const string UmbralDisponibilidad = "UMBRAL_DISPONIBILIDAD_FLOTA_CRITICA";
    public const string DiasRetornoTercerizado = "DIAS_RETORNO_TERCERIZADO";
    public const string VidaUtilAnios = "VIDA_UTIL_ANIOS";
    public const string FamiliasRespirador = "FAMILIAS_RESPIRADOR";
}
