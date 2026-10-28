namespace Nodus.Api.Infrastructure;

/// <summary>Logo institucional usado en documentos PDF y Excel (Recursos/logo.png).</summary>
public static class Logo
{
    private static readonly Lazy<byte[]?> Cache = new(() =>
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Recursos", "logo.png");
        return File.Exists(ruta) ? File.ReadAllBytes(ruta) : null;
    });

    public static byte[]? Png => Cache.Value;
}
