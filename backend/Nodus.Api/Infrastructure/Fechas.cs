namespace Nodus.Api.Infrastructure;

/// <summary>Fechas de negocio en hora de Córdoba (la base guarda timestamptz en UTC).</summary>
public static class Fechas
{
    public static readonly TimeZoneInfo Zona = Resolver();

    private static TimeZoneInfo Resolver()
    {
        foreach (var id in new[] { "America/Argentina/Cordoba", "America/Argentina/Buenos_Aires", "Argentina Standard Time" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "ART", "ART");
    }

    public static DateTime AhoraLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona);
    public static DateOnly Hoy => DateOnly.FromDateTime(AhoraLocal);

    public static DateTime ALocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    /// <summary>Inicio (inclusive) y fin (exclusivo) de un mes local, en UTC.</summary>
    public static (DateTime desdeUtc, DateTime hastaUtc) RangoMesUtc(int anio, int mes)
    {
        var desde = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var hasta = desde.AddMonths(1);
        return (TimeZoneInfo.ConvertTimeToUtc(desde, Zona), TimeZoneInfo.ConvertTimeToUtc(hasta, Zona));
    }

    public static (DateTime desdeUtc, DateTime hastaUtc) RangoDiaUtc(DateOnly dia)
    {
        var desde = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(desde, Zona), TimeZoneInfo.ConvertTimeToUtc(desde.AddDays(1), Zona));
    }

    public static readonly string[] MesesEs =
        { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
}
