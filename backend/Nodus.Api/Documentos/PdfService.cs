using Nodus.Api.Domain;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;
using Nodus.Api.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Nodus.Api.Documentos;

/// <summary>Documentos PDF generados al vuelo con QuestPDF.</summary>
public class PdfService
{
    private const string Institucion = "HOSPITAL MATERNO NEONATAL · CÓRDOBA";
    private const string Sector = "Sector Bioingeniería — Sistema NODUS";
    private static readonly string Azul = "#14324F";

    private static string F(DateTime utc) => Fechas.ALocal(utc).ToString("dd/MM/yyyy HH:mm");
    private static string F(DateTime? utc) => utc is DateTime d ? F(d) : "—";
    private static string F(DateOnly d) => d.ToString("dd/MM/yyyy");
    private static string F(DateOnly? d) => d is DateOnly x ? F(x) : "—";
    private static string T(Enum e) => e.ToString().Replace('_', ' ');

    private static byte[] Documento(string titulo, string subtitulo, Action<IContainer> contenido, bool apaisado = false) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(apaisado ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    if (Logo.Png is byte[] logo)
                        row.ConstantItem(105).PaddingRight(14).AlignMiddle().Image(logo).FitWidth();
                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Item().Text(Institucion).Bold().FontSize(12).FontColor(Azul);
                        c.Item().Text(Sector).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(160).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text($"Emitido: {Fechas.AhoraLocal:dd/MM/yyyy HH:mm}").FontSize(8);
                    });
                });
                col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Azul);
                col.Item().PaddingTop(8).Text(titulo).Bold().FontSize(15);
                if (!string.IsNullOrEmpty(subtitulo))
                    col.Item().Text(subtitulo).FontSize(10).FontColor(Colors.Grey.Darken2);
                col.Item().PaddingBottom(8);
            });

            page.Content().Element(contenido);

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Documento generado por NODUS · Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        })).GeneratePdf();

    private static void Campos(IContainer c, params (string label, string? valor)[] campos) =>
        c.Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.ConstantColumn(150); cd.RelativeColumn(); });
            foreach (var (label, valor) in campos)
            {
                t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(label).SemiBold();
                t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(valor ?? "—");
            }
        });

    private static void Tabla(IContainer c, string[] encabezados, float[] anchos, IEnumerable<string[]> filas, float fuente = 8)
    {
        var lista = filas.ToList();
        if (lista.Count == 0)
        {
            c.PaddingVertical(4).Text("Sin registros en el período.").Italic().FontColor(Colors.Grey.Darken1).FontSize(fuente + 1);
            return;
        }
        c.Table(t =>
        {
            t.ColumnsDefinition(cd => { foreach (var a in anchos) cd.RelativeColumn(a); });
            t.Header(h =>
            {
                foreach (var e in encabezados)
                    h.Cell().Background(Azul).Padding(3).Text(e).Bold().FontSize(fuente).FontColor(Colors.White);
            });
            var i = 0;
            foreach (var fila in lista)
            {
                var fondo = i++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                foreach (var celda in fila)
                    t.Cell().Background(fondo).Padding(3).Text(celda).FontSize(fuente);
            }
        });
    }

    private static void Seccion(ColumnDescriptor col, string titulo) =>
        col.Item().PaddingTop(12).PaddingBottom(4).Text(titulo).Bold().FontSize(11).FontColor(Azul);

    private static void Firmas(ColumnDescriptor col, params string[] firmantes) =>
        col.Item().PaddingTop(55).Row(row =>
        {
            foreach (var f in firmantes)
            {
                row.RelativeItem().PaddingHorizontal(12).Column(c =>
                {
                    c.Item().LineHorizontal(0.8f);
                    c.Item().AlignCenter().Text(f).FontSize(8);
                    c.Item().AlignCenter().Text("Firma y aclaración").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            }
        });

    public byte[] RemitoTercerizado(OtDto ot, EquipoListDto equipo)
    {
        var t = ot.Tercerizado ?? throw new BusinessException("La OT no es tercerizada.");
        return Documento($"Remito de salida N° T-{t.Id:D5}", "Envío de equipo a reparación externa", c => c.Column(col =>
        {
            Seccion(col, "Equipo");
            col.Item().Element(x => Campos(x,
                ("Código patrimonial", equipo.CodigoPatrimonial),
                ("Tipo", equipo.Familia),
                ("Marca / Modelo", $"{equipo.Marca} {equipo.Modelo}"),
                ("N° de serie", equipo.NroSerie),
                ("Servicio de origen", equipo.ServicioActual)));

            Seccion(col, "Destino y plazos");
            col.Item().Element(x => Campos(x,
                ("Proveedor / fábrica", t.Proveedor),
                ("Cobertura", t.EnGarantia ? "EN GARANTÍA" : "Fuera de garantía"),
                ("Fecha de envío", F(t.FechaEnvio)),
                ("Retorno probable", F(t.FechaProbableVuelta)),
                ("Orden de trabajo", $"OT #{ot.Id} ({T(ot.Tipo)})")));

            Seccion(col, "Falla / motivo del envío");
            col.Item().Text(ot.DescripcionFalla ?? ot.DescripcionTareaTecnica ?? "—");

            Firmas(col, "Entrega — Bioingeniería", "Recibe — Transporte / Proveedor");
        }));
    }

    public byte[] OrdenTrabajo(OtDto ot) =>
        Documento($"Orden de Trabajo #{ot.Id}", $"{T(ot.Tipo)} · {T(ot.Estado)}", c => c.Column(col =>
        {
            col.Item().Element(x => Campos(x,
                ("Equipo", $"{ot.CodigoPatrimonial} — {ot.Equipo}"),
                ("Servicio", ot.Servicio),
                ("Apertura", F(ot.FechaApertura)),
                ("Inicio en taller", F(ot.FechaInicioTaller)),
                ("Cierre", F(ot.FechaCierre)),
                ("Técnico asignado", ot.TecnicoAsignado),
                ("Creada por", ot.Creador)));

            if (ot.DescripcionFalla != null)
            {
                Seccion(col, "Falla reportada");
                col.Item().Text(ot.DescripcionFalla);
            }
            Seccion(col, "Tarea técnica realizada");
            col.Item().Text(ot.DescripcionTareaTecnica ?? "—");

            Seccion(col, "Prueba de seguridad eléctrica");
            col.Item().Element(x => Campos(x,
                ("Resultado", T(ot.SeguridadElectrica)),
                ("Observaciones", ot.ObservacionesSeguridadElectrica)));

            Seccion(col, "Insumos consumidos");
            col.Item().Element(x => Tabla(x, new[] { "Insumo", "Cantidad", "Fecha", "Registró" }, new[] { 4f, 1, 2, 3 },
                ot.Consumos.Select(k => new[] { k.Insumo, k.Cantidad.ToString(), F(k.Fecha), k.Usuario })));

            Firmas(col, "Técnico", "Bioingeniero responsable");
        }));

    public byte[] NotaCompra(SolicitudCompraDto s, InsumoDto insumo) =>
        Documento($"Nota de Compra N° NC-{s.Id:D5}", "Solicitud de adquisición de insumos", c => c.Column(col =>
        {
            col.Item().AlignRight().Text($"Córdoba, {Fechas.ALocal(s.Fecha):dd/MM/yyyy}");
            col.Item().PaddingTop(10).Text("Al Área de Compras y Suministros:").SemiBold();
            col.Item().PaddingTop(6).Text(
                "Por medio de la presente, el Sector de Bioingeniería solicita la adquisición del insumo detallado a continuación, " +
                "necesario para el mantenimiento de los equipos de soporte ventilatorio del hospital.");

            Seccion(col, "Detalle");
            col.Item().Element(x => Tabla(x, new[] { "Insumo", "Categoría", "Stock actual", "Stock mínimo", "Estado", "Cantidad solicitada" },
                new[] { 4f, 2, 1.5f, 1.5f, 2, 2 },
                new[] { new[] { insumo.Nombre, insumo.Categoria, insumo.StockActual.ToString(), insumo.StockMinimo.ToString(),
                    T(insumo.EstadoStock), s.Cantidad.ToString() } }, 9));

            col.Item().PaddingTop(10).Text($"Solicitado por: {s.Usuario}");
            Firmas(col, "Solicitante — Bioingeniería", "Autorización — Dirección");
        }));

    public byte[] RemitoPrestamo(PrestamoDto p)
    {
        var otorgado = p.Tipo == TipoDireccionPrestamo.OTORGADO;
        return Documento($"Remito de préstamo N° P-{p.Id:D5}",
            otorgado ? "Salida de equipo en préstamo a otra institución" : "Recepción de equipo en préstamo de otra institución",
            c => c.Column(col =>
            {
                col.Item().Element(x => Campos(x,
                    (otorgado ? "Institución receptora" : "Institución prestadora", p.Hospital),
                    ("Equipo", p.DescripcionEquipo),
                    ("Código patrimonial", p.CodigoPatrimonial ?? "Equipo externo (no inventariado)"),
                    ("Fecha de salida", F(p.FechaSalida)),
                    ("Devolución estimada", F(p.FechaDevolucionEstimada)),
                    ("Responsable retiro/entrega", p.Responsable),
                    ("Accesorios", p.Accesorios)));

                Seccion(col, "Estado técnico a la entrega");
                col.Item().Text(p.EstadoTecnicoEntrega);

                if (p.Estado == EstadoPrestamo.FINALIZADO)
                {
                    Seccion(col, "Devolución");
                    col.Item().Element(x => Campos(x,
                        ("Fecha de devolución", F(p.FechaDevolucionReal)),
                        ("Estado técnico a la recepción", p.EstadoTecnicoRecepcion)));
                }

                Firmas(col, otorgado ? "Entrega — Bioingeniería HMN" : "Entrega — Institución prestadora",
                    otorgado ? "Recibe — Institución receptora" : "Recibe — Bioingeniería HMN");
            }));
    }

    public byte[] NotaInstitucional(PrestamoDto p)
    {
        var otorgado = p.Tipo == TipoDireccionPrestamo.OTORGADO;
        return Documento($"Nota institucional — Préstamo N° P-{p.Id:D5}", "", c => c.Column(col =>
        {
            col.Item().AlignRight().Text($"Córdoba, {Fechas.Hoy:dd/MM/yyyy}");
            col.Item().PaddingTop(14).Text($"Sres. {p.Hospital}").SemiBold();
            col.Item().Text("Presente").SemiBold();
            col.Item().PaddingTop(12).Text(t =>
            {
                t.Span("De nuestra mayor consideración: ");
                t.Span(otorgado
                    ? $"nos dirigimos a ustedes a fin de formalizar el préstamo del equipo {p.DescripcionEquipo}, código patrimonial {p.CodigoPatrimonial}, " +
                      $"propiedad del Hospital Materno Neonatal, a partir del día {F(p.FechaSalida)} y con fecha de devolución prevista para el {F(p.FechaDevolucionEstimada)}."
                    : $"nos dirigimos a ustedes a fin de agradecer y formalizar la recepción en préstamo del equipo {p.DescripcionEquipo}, " +
                      $"a partir del día {F(p.FechaSalida)} y con fecha de devolución prevista para el {F(p.FechaDevolucionEstimada)}.");
            });
            col.Item().PaddingTop(8).Text(
                $"El equipo se entrega con el siguiente estado técnico: {p.EstadoTecnicoEntrega}." +
                (string.IsNullOrWhiteSpace(p.Accesorios) ? "" : $" Accesorios incluidos: {p.Accesorios}."));
            col.Item().PaddingTop(8).Text(otorgado
                ? "La institución receptora se compromete a utilizarlo conforme a las indicaciones del fabricante, no realizar intervenciones técnicas sin autorización de nuestro Sector de Bioingeniería y devolverlo en las mismas condiciones en que fue recibido."
                : "Nuestra institución se compromete a utilizarlo conforme a las indicaciones del fabricante y a devolverlo en las mismas condiciones en que fue recibido.");
            col.Item().PaddingTop(8).Text("Sin otro particular, saludamos a ustedes muy atentamente.");
            Firmas(col, "Jefatura de Bioingeniería", "Dirección del Hospital");
        }));
    }

    public byte[] Consolidado(ConsolidadoData d) =>
        Documento("Consolidado General Mensual de Activos y Mantenimiento",
            $"Período: {d.Periodo} · Hospital {d.Hospital} · Generado por {d.GeneradoPor}", c => c.Column(col =>
        {
            Seccion(col, "1. Indicadores");
            col.Item().Element(x => Campos(x,
                ("Disponibilidad de flota (actual)", $"{d.DisponibilidadGlobal:0.0}% (umbral crítico {d.Umbral:0}%)"),
                ("MTTR del período", d.MttrHorasMes is decimal m ? $"{m:0.0} h" : "Sin OT correctivas cerradas"),
                ("OT abiertas en el período", d.OtsAbiertasMes.ToString()),
                ("OT cerradas en el período", d.OtsCerradasMes.ToString()),
                ("Reportes de falla recibidos", $"{d.ReportesMes} ({d.ReportesDescartadosMes} descartados)"),
                ("Altas / Bajas / Traslados", $"{d.Altas.Count} / {d.Bajas.Count} / {d.Traslados.Count}")));

            Seccion(col, "2. Disponibilidad por tipo de equipo");
            col.Item().Element(x => Tabla(x, new[] { "Tipo", "Total", "Operativos", "Disponibilidad", "Semáforo" }, new[] { 4f, 1, 1, 1.5f, 1.5f },
                d.PorTipo.Select(p => new[] { p.Familia, p.Total.ToString(), p.Operativos.ToString(), $"{p.Porcentaje:0.0}%", p.Semaforo })));

            Seccion(col, "3. Inventario de activos");
            col.Item().Element(x => Tabla(x,
                new[] { "Código", "Tipo", "Marca", "Modelo", "N° serie", "Servicio", "Estado", "Garantía", "Próx. preventivo" },
                new[] { 2.4f, 2.2f, 1.6f, 1.6f, 1.6f, 1.8f, 1.8f, 1.2f, 1.3f },
                d.Inventario.Select(e => new[] { e.Codigo, e.Familia, e.Marca, e.Modelo, e.NroSerie, e.Servicio, T(e.Estado), F(e.Garantia), F(e.Preventivo) }), 7));

            Seccion(col, "4. Órdenes de trabajo con actividad en el período");
            col.Item().Element(x => Tabla(x,
                new[] { "OT", "Equipo", "Tipo", "Estado", "Apertura", "Cierre", "Técnico", "Seg. eléctrica", "Tarea" },
                new[] { 0.6f, 2.2f, 1.4f, 1.3f, 1.6f, 1.6f, 1.8f, 1.3f, 3.5f },
                d.Ordenes.Select(o => new[] { o.Id.ToString(), o.Codigo, T(o.Tipo), T(o.Estado), F(o.Apertura), F(o.Cierre), o.Tecnico ?? "—",
                    T(o.SegElectrica), o.Tarea ?? "—" }), 7));

            Seccion(col, "5. Altas patrimoniales");
            col.Item().Element(x => Tabla(x, new[] { "Fecha", "Código", "Detalle" }, new[] { 1.5f, 2.5f, 7 },
                d.Altas.Select(a => new[] { F(a.Fecha), a.Codigo, a.Detalle })));

            Seccion(col, "6. Bajas patrimoniales");
            col.Item().Element(x => Tabla(x, new[] { "Fecha", "Código", "Motivo / observaciones", "Bioingeniero" }, new[] { 1.5f, 2.5f, 5, 2.5f },
                d.Bajas.Select(b => new[] { Fechas.ALocal(b.Fecha).ToString("dd/MM/yyyy"), b.Codigo, b.Detalle.Replace('_', ' '), b.Usuario })));

            Seccion(col, "7. Traslados entre servicios");
            col.Item().Element(x => Tabla(x, new[] { "Fecha", "Código", "Movimiento", "Usuario" }, new[] { 1.8f, 2.5f, 4, 2.5f },
                d.Traslados.Select(t => new[] { F(t.Fecha), t.Codigo, t.Detalle, t.Usuario })));

            Seccion(col, "8. Préstamos interinstitucionales vigentes en el período");
            col.Item().Element(x => Tabla(x, new[] { "N°", "Tipo", "Estado", "Equipo", "Institución", "Salida", "Dev. estimada", "Dev. real" },
                new[] { 0.6f, 1.3f, 1.3f, 2.8f, 3, 1.3f, 1.3f, 1.3f },
                d.Prestamos.Select(p => new[] { p.Id.ToString(), T(p.Tipo), T(p.Estado), p.Equipo, p.Hospital, F(p.Salida), F(p.Estimada), F(p.Real) })));

            Seccion(col, "9. Insumos");
            col.Item().Element(x => Tabla(x, new[] { "Insumo", "Consumido", "Repuesto", "Stock actual", "Estado" }, new[] { 4f, 1.2f, 1.2f, 1.2f, 1.6f },
                d.Insumos.Select(i => new[] { i.Insumo, i.Consumido.ToString(), i.Repuesto.ToString(), i.StockActual.ToString(), T(i.Estado) })));

            Firmas(col, "Jefatura de Bioingeniería", "Dirección del Hospital");
        }), apaisado: true);
}
