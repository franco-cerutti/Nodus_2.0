using ClosedXML.Excel;
using Nodus.Api.Dtos;
using Nodus.Api.Infrastructure;
using Nodus.Api.Services;

namespace Nodus.Api.Documentos;

/// <summary>Reportes en Excel generados al vuelo: consolidado mensual y tablero de control.</summary>
public class ExcelService
{
    private static readonly XLColor Azul = XLColor.FromHtml("#14324F");

    private static object L(DateTime utc) => Fechas.ALocal(utc);
    private static object L(DateTime? utc) => utc is DateTime d ? Fechas.ALocal(d) : "";
    private static object D(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);
    private static object D(DateOnly? d) => d is DateOnly x ? x.ToDateTime(TimeOnly.MinValue) : "";
    private static object N(decimal? v) => v is decimal x ? x : "";
    private static string T(Enum e) => e.ToString().Replace('_', ' ');

    public byte[] Consolidado(ConsolidadoData d)
    {
        using var wb = new XLWorkbook();
        var r = wb.AddWorksheet("Resumen");
        var fila = Encabezado(r, "Consolidado General Mensual de Activos y Mantenimiento",
            $"Hospital Materno Neonatal ({d.Hospital}) · Sector Bioingeniería · Período {d.Periodo}",
            $"Generado {d.GeneradoLocal:dd/MM/yyyy HH:mm} por {d.GeneradoPor}");

        fila = Pares(r, fila, new (string, object)[]
        {
            ("Disponibilidad de flota (%)", d.DisponibilidadGlobal),
            ("Umbral crítico (%)", d.Umbral),
            ("MTTR del período (h)", N(d.MttrHorasMes)),
            ("OT abiertas en el período", d.OtsAbiertasMes),
            ("OT cerradas en el período", d.OtsCerradasMes),
            ("Reportes de falla", d.ReportesMes),
            ("Reportes descartados", d.ReportesDescartadosMes),
            ("Altas", d.Altas.Count),
            ("Bajas", d.Bajas.Count),
            ("Traslados", d.Traslados.Count),
        });
        Titulo(r, ++fila, "Disponibilidad por tipo");
        Tabla(r, fila + 1, new[] { "Tipo", "Total", "Operativos", "Disponibilidad (%)", "Semáforo" },
            d.PorTipo.Select(p => new object[] { p.Familia, p.Total, p.Operativos, p.Porcentaje, p.Semaforo }));
        r.Columns(2, 6).AdjustToContents(5, 400);

        Hoja(wb, "Inventario",
            new[] { "Código patrimonial", "Tipo", "Marca", "Modelo", "N° serie", "Servicio actual", "Estado", "Vto. garantía", "Próx. preventivo" },
            d.Inventario.Select(e => new object[] { e.Codigo, e.Familia, e.Marca, e.Modelo, e.NroSerie, e.Servicio, T(e.Estado), D(e.Garantia), D(e.Preventivo) }));
        Hoja(wb, "Órdenes de trabajo",
            new[] { "OT", "Equipo", "Tipo", "Estado", "Apertura", "Cierre", "Técnico", "Seguridad eléctrica", "Tarea técnica" },
            d.Ordenes.Select(o => new object[] { o.Id, o.Codigo, T(o.Tipo), T(o.Estado), L(o.Apertura), L(o.Cierre), o.Tecnico ?? "", T(o.SegElectrica), o.Tarea ?? "" }));
        Hoja(wb, "Altas", new[] { "Fecha", "Código", "Detalle" },
            d.Altas.Select(a => new object[] { L(a.Fecha), a.Codigo, a.Detalle }));
        Hoja(wb, "Bajas", new[] { "Fecha", "Código", "Motivo / observaciones", "Bioingeniero" },
            d.Bajas.Select(b => new object[] { Fechas.ALocal(b.Fecha).Date, b.Codigo, b.Detalle, b.Usuario }));
        Hoja(wb, "Traslados", new[] { "Fecha", "Código", "Movimiento", "Usuario" },
            d.Traslados.Select(t => new object[] { L(t.Fecha), t.Codigo, t.Detalle, t.Usuario }));
        Hoja(wb, "Préstamos",
            new[] { "N°", "Tipo", "Estado", "Equipo", "Institución", "Salida", "Devolución estimada", "Devolución real" },
            d.Prestamos.Select(p => new object[] { p.Id, T(p.Tipo), T(p.Estado), p.Equipo, p.Hospital, D(p.Salida), D(p.Estimada), D(p.Real) }));
        Hoja(wb, "Insumos", new[] { "Insumo", "Consumido", "Repuesto", "Stock actual", "Estado" },
            d.Insumos.Select(i => new object[] { i.Insumo, i.Consumido, i.Repuesto, i.StockActual, T(i.Estado) }));

        return Guardar(wb);
    }

    public byte[] Tablero(DashboardDto d, string filtros, string generadoPor)
    {
        using var wb = new XLWorkbook();
        var r = wb.AddWorksheet("Resumen");
        var fila = Encabezado(r, "Tablero de Control de Activos de Soporte Ventilatorio",
            $"Hospital Materno Neonatal · Sector Bioingeniería · {filtros}",
            $"Generado {Fechas.AhoraLocal:dd/MM/yyyy HH:mm} por {generadoPor}");

        var k = d.Kpis;
        fila = Pares(r, fila, new (string, object)[]
        {
            ("Total de equipos", k.TotalEquipos),
            ("Equipos operativos", k.Operativos),
            ("Disponibilidad general (%)", k.Disponibilidad),
            ("Umbral crítico (%)", d.Umbral),
            ("Estado", k.Semaforo == "CRITICO" ? "CRÍTICO" : "Normal"),
            ("Respiradores libres para urgencias", $"{k.RespiradoresLibres} de {k.RespiradoresTotal}"),
            ("OT abiertas", k.OtsAbiertas),
            ("Reportes de falla pendientes", k.ReportesPendientes),
            ("Alertas abiertas", k.AlertasAbiertas),
            ("OT bloqueadas por falta de stock", d.OtsBloqueadas.PorStock),
            ("OT bloqueadas por seguridad eléctrica", d.OtsBloqueadas.PorSeguridadElectrica),
            ("MTTR correctivo (días)", N(d.Mttr.GlobalCorrectivo)),
            ("MTTR preventivo (días)", N(d.Mttr.GlobalPreventivo)),
            ($"Parque que supera {d.Parque.VidaUtilAnios} años (%)", d.Parque.PorcentajeExcedido),
            ("Antigüedad promedio (años)", d.Parque.EdadPromedio),
            ("Unidades de insumos consumidas", d.Consumo.TotalUnidades),
            ("Servicios técnicos externos", d.Consumo.TotalServiciosExternos),
            ("Fallas en el período", d.Heatmap.TotalFallasPeriodo),
        });
        r.Columns(2, 6).AdjustToContents(5, 400);

        Hoja(wb, "Disponibilidad por tipo", new[] { "Tipo de equipo", "Operativos", "Total", "Disponibilidad (%)", "Estado" },
            d.PorTipo.Select(t => new object[] { t.Familia, t.Operativos, t.Total, t.Total == 0 ? "" : t.Porcentaje,
                t.Semaforo switch { "CRITICO" => "Crítico", "OK" => "Normal", _ => "Sin equipos" } }));

        Hoja(wb, "Grilla por servicio", new[] { "Servicio / Área", "Operativos", "Fuera de servicio", "En mantenimiento", "Prestados", "Total" },
            d.Grilla.Append(d.GrillaTotal).Select(g => new object[] { g.Servicio, g.Operativos, g.FueraDeServicio, g.EnMantenimiento, g.EnPrestamo, g.Total }),
            resaltarUltima: true);

        Hoja(wb, "OT bloqueadas", new[] { "OT", "Equipo", "Descripción", "Servicio", "Motivo", "Detalle", "Apertura" },
            d.OtsBloqueadas.Detalle.Select(o => new object[] { o.IdOt, o.CodigoPatrimonial, o.Equipo, o.Servicio,
                o.Motivo == "STOCK" ? "Falta de stock" : "Seguridad eléctrica no aprobada", o.Detalle, L(o.Apertura) }));

        Hoja(wb, "Tareas del día", new[] { "Categoría", "Referencia", "Detalle", "Fecha", "Días restantes" },
            d.Tareas.Preventivos.Select(t => Tarea("Preventivo", t))
                .Concat(d.Tareas.Prestamos.Select(t => Tarea("Préstamo", t)))
                .Concat(d.Tareas.Tercerizados.Select(t => Tarea("Tercerizado", t))));

        Hoja(wb, "Fallas por marca", new[] { "Marca", "Fallas" },
            d.FallasPorMarca.Select(f => new object[] { f.Nombre, f.Cantidad }));

        Hoja(wb, "MTTR", new[] { "Tipo de equipo", "Correctivo (días)", "OT correctivas", "Preventivo (días)", "OT preventivas" },
            d.Mttr.PorFamilia.Select(m => new object[] { m.Familia, N(m.Correctivo), m.NCorrectivo, N(m.Preventivo), m.NPreventivo }));

        Hoja(wb, "Parque tecnológico", new[] { "Año", $"Dentro de vida útil (≤ {d.Parque.VidaUtilAnios} años)", $"Supera vida útil (> {d.Parque.VidaUtilAnios} años)", "Total" },
            d.Parque.Serie.Select(p => new object[] { p.Anio, p.Vigentes, p.Excedidos, p.Vigentes + p.Excedidos }));

        var consumo = Hoja(wb, "Consumo de recursos", new[] { "Tipo de equipo", "Unidades de insumos", "Servicios externos" },
            d.Consumo.PorFamilia.Select(c => new object[] { c.Familia, c.UnidadesInsumos, c.ServiciosExternos }));
        Tabla(consumo, d.Consumo.PorFamilia.Count + 4, new[] { "Insumo", "Unidades consumidas" },
            d.Consumo.TopInsumos.Select(i => new object[] { i.Nombre, i.Cantidad }));
        consumo.Columns(1, 3).AdjustToContents();

        var h = d.Heatmap;
        var calor = Hoja(wb, "Mapa de calor", new[] { "Tipo de equipo" }.Concat(h.Meses).Append("Total").ToArray(),
            h.Familias.Select(f =>
            {
                var valores = h.Meses.Select(m => (object)(h.PorMes.FirstOrDefault(c => c.Familia == f && c.Mes == m)?.Fallas ?? 0)).ToList();
                return new object[] { f }.Concat(valores).Append(valores.Sum(v => (int)v)).ToArray();
            }));
        if (h.Familias.Count > 0)
        {
            var rango = calor.Range(2, 2, h.Familias.Count + 1, h.Meses.Count + 1);
            rango.AddConditionalFormat().ColorScale().LowestValue(XLColor.FromHtml("#F1F5F9")).HighestValue(XLColor.FromHtml("#C62828"));
        }

        string[] dias = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        var horas = Hoja(wb, "Fallas por hora", new[] { "Día" }.Concat(Enumerable.Range(0, 24).Select(x => $"{x:00}h")).ToArray(),
            h.DiaHora.Select((fila, i) => new object[] { dias[i] }.Concat(fila.Cast<object>()).ToArray()));
        horas.Range(2, 2, 8, 25).AddConditionalFormat().ColorScale().LowestValue(XLColor.FromHtml("#F1F5F9")).HighestValue(XLColor.FromHtml("#C62828"));

        return Guardar(wb);
    }

    private static object[] Tarea(string categoria, TareaDiaDto t) =>
        new object[] { categoria, t.Titulo, t.Detalle, D(t.Fecha), t.DiasRestantes };

    private static int Encabezado(IXLWorksheet ws, string titulo, string linea1, string linea2)
    {
        var col = 1;
        if (Logo.Png is byte[] png)
        {
            ws.AddPicture(new MemoryStream(png)).MoveTo(ws.Cell(1, 1)).WithSize(158, 56);
            ws.Column(1).Width = 24;
            col = 2;
        }
        ws.Cell(1, col).Value = titulo;
        ws.Cell(1, col).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(Azul);
        ws.Cell(2, col).Value = linea1;
        ws.Cell(3, col).Value = linea2;
        ws.Cell(3, col).Style.Font.SetFontColor(XLColor.Gray);
        return 5;
    }

    private static int Pares(IXLWorksheet ws, int fila, (string, object)[] pares)
    {
        foreach (var (clave, valor) in pares)
        {
            ws.Cell(fila, 2).Value = clave;
            ws.Cell(fila, 3).Value = XLCellValue.FromObject(valor);
            ws.Cell(fila, 3).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            fila++;
        }
        return fila;
    }

    private static void Titulo(IXLWorksheet ws, int fila, string texto)
    {
        ws.Cell(fila, 2).Value = texto;
        ws.Cell(fila, 2).Style.Font.SetBold().Font.SetFontColor(Azul);
    }

    private static IXLWorksheet Hoja(XLWorkbook wb, string nombre, string[] encabezados, IEnumerable<object[]> filas, bool resaltarUltima = false)
    {
        var ws = wb.AddWorksheet(nombre);
        var f = Tabla(ws, 1, encabezados, filas);
        ws.SheetView.FreezeRows(1);
        if (f > 2) ws.Range(1, 1, f - 1, encabezados.Length).SetAutoFilter();
        if (resaltarUltima && f > 2)
            ws.Range(f - 1, 1, f - 1, encabezados.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E2E8F0"));
        ws.Columns().AdjustToContents(1, Math.Min(f, 300));
        return ws;
    }

    private static int Tabla(IXLWorksheet ws, int filaInicio, string[] encabezados, IEnumerable<object[]> filas)
    {
        for (var c = 0; c < encabezados.Length; c++)
        {
            var cell = ws.Cell(filaInicio, c + 1);
            cell.Value = encabezados[c];
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(Azul);
        }

        var f = filaInicio + 1;
        foreach (var fila in filas)
        {
            for (var c = 0; c < fila.Length; c++)
            {
                var cell = ws.Cell(f, c + 1);
                cell.Value = XLCellValue.FromObject(fila[c]);
                if (fila[c] is DateTime dt)
                    cell.Style.DateFormat.Format = dt.TimeOfDay == TimeSpan.Zero ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm";
            }
            f++;
        }
        return f;
    }

    private static byte[] Guardar(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
