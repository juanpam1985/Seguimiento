using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Seguimiento.Data;
using Seguimiento.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Seguimiento.Controllers
{
    public class ReporteEmbarqueController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReporteEmbarqueController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult ExportarExcel(int? marcaId, string filtro, string q)
        {
            try
            {
                var empresaId = HttpContext.Session.GetInt32("Id");

                if (empresaId == null)
                    return RedirectToAction("Login", "Account");

                var embarques = _context.Embarques
                    .Include(e => e.Empresa)
                    .Include(e => e.Respuestas)
                        .ThenInclude(r => r.Pregunta)
                    .Include(e => e.EmbarqueModelos)
                        .ThenInclude(em => em.Modelo)
                    .AsNoTracking()
                    .Where(e => e.EmpresaId == empresaId.Value)
                    .ToList();

                if (marcaId.HasValue)
                {
                    embarques = embarques
                        .Where(x =>
                        {
                            var respuestas = x.Respuestas?.ToList() ?? new List<Respuesta>();
                            var brand = GetValor(respuestas, "BRAND");
                            return brand == marcaId.Value.ToString();
                        })
                        .ToList();
                }

                // ==========================================
                // BÚSQUEDA GLOBAL
                // ==========================================
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var palabras = q.Trim()
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim().ToLower())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();

                    var subtramitesEmpresaIds = embarques
                        .Where(e => e.EsSubtramite)
                        .Select(e => e.Id)
                        .ToList();

                    var packings = _context.PackingLists
                        .Include(p => p.Detalles)
                        .Where(p => p.Activo && subtramitesEmpresaIds.Contains(p.SubtramiteId))
                        .AsNoTracking()
                        .ToList();

                    bool CoincideTexto(string origen, string buscado)
                    {
                        if (string.IsNullOrWhiteSpace(origen) || string.IsNullOrWhiteSpace(buscado))
                            return false;

                        return origen.Trim().ToLower().Contains(buscado.Trim().ToLower());
                    }

                    bool CoincideEmbarque(Embarque e, string palabra)
                    {
                        if (e.Empresa != null && CoincideTexto(e.Empresa.Nombre, palabra))
                            return true;

                        if (CoincideTexto(e.CreadoPor, palabra) || CoincideTexto(e.ModificadoPor, palabra))
                            return true;

                        var respuestas = e.Respuestas?.ToList() ?? new List<Respuesta>();

                        foreach (var r in respuestas)
                        {
                            if (r.Pregunta != null && CoincideTexto(r.Pregunta.Nombre, palabra))
                                return true;

                            if (CoincideTexto(r.Valor, palabra))
                                return true;
                        }

                        return false;
                    }

                    bool CoincidePacking(PackingList p, string palabra)
                    {
                        if (CoincideTexto(p.RequestNo, palabra))
                            return true;

                        if (p.FechaEmbarque.HasValue && p.FechaEmbarque.Value.ToString("dd/MM/yyyy").Contains(palabra))
                            return true;

                        if (p.FechaArribo.HasValue && p.FechaArribo.Value.ToString("dd/MM/yyyy").Contains(palabra))
                            return true;

                        var detalles = p.Detalles ?? new List<PackingListDetalle>();

                        foreach (var d in detalles)
                        {
                            if (
                                CoincideTexto(d.OriginPort, palabra) ||
                                CoincideTexto(d.DestinationPort, palabra) ||
                                CoincideTexto(d.Ship, palabra) ||
                                CoincideTexto(d.InvoiceNumber, palabra) ||
                                CoincideTexto(d.BL, palabra) ||
                                CoincideTexto(d.VinNo, palabra) ||
                                CoincideTexto(d.ModelPacking, palabra) ||
                                CoincideTexto(d.ModelSRI, palabra) ||
                                CoincideTexto(d.ModelHomologacion, palabra) ||
                                CoincideTexto(d.EngineCode, palabra) ||
                                CoincideTexto(d.EngineNumber, palabra) ||
                                CoincideTexto(d.ColorCodigo, palabra) ||
                                CoincideTexto(d.ColorName, palabra) ||
                                CoincideTexto(d.ColorLocal, palabra) ||
                                CoincideTexto(d.ModelYear, palabra) ||
                                CoincideTexto(d.Incoterm, palabra) ||
                                d.Linea.ToString().Contains(palabra)
                            )
                            {
                                return true;
                            }

                            if (d.NetWeightKg.HasValue && d.NetWeightKg.Value.ToString().Contains(palabra))
                                return true;

                            if (d.CBM.HasValue && d.CBM.Value.ToString().Contains(palabra))
                                return true;

                            if (d.Precio.HasValue && d.Precio.Value.ToString().Contains(palabra))
                                return true;

                            if (d.Flete.HasValue && d.Flete.Value.ToString().Contains(palabra))
                                return true;

                            if (d.Seguro.HasValue && d.Seguro.Value.ToString().Contains(palabra))
                                return true;
                        }

                        return false;
                    }

                    bool CoincideTodo(Embarque e)
                    {
                        foreach (var palabra in palabras)
                        {
                            var coincideDirecto = CoincideEmbarque(e, palabra);

                            var coincidePacking = packings.Any(p =>
                                p.SubtramiteId == e.Id && CoincidePacking(p, palabra));

                            if (!(coincideDirecto || coincidePacking))
                                return false;
                        }

                        return true;
                    }

                    var embarquesCoincidentes = embarques
                        .Where(e => CoincideTodo(e))
                        .ToList();

                    var idsPrincipales = new HashSet<int>();
                    var idsSubtramites = new HashSet<int>();

                    foreach (var e in embarquesCoincidentes)
                    {
                        if (e.EsSubtramite)
                        {
                            idsSubtramites.Add(e.Id);

                            if (e.EmbarquePadreId.HasValue)
                                idsPrincipales.Add(e.EmbarquePadreId.Value);
                        }
                        else
                        {
                            idsPrincipales.Add(e.Id);
                        }
                    }

                    var subtramitesConPacking = packings
                        .Where(p => palabras.All(pal => CoincidePacking(p, pal)))
                        .Select(p => p.SubtramiteId)
                        .Distinct()
                        .ToList();

                    foreach (var subId in subtramitesConPacking)
                    {
                        idsSubtramites.Add(subId);

                        var sub = embarques.FirstOrDefault(x => x.Id == subId);
                        if (sub?.EmbarquePadreId != null)
                            idsPrincipales.Add(sub.EmbarquePadreId.Value);
                    }

                    embarques = embarques
                        .Where(e =>
                            (!e.EsSubtramite && idsPrincipales.Contains(e.Id)) ||
                            (e.EsSubtramite && idsSubtramites.Contains(e.Id)))
                        .ToList();
                }

                // El reporte exporta PRINCIPALES, pero respetando la búsqueda global
                var principales = embarques
                    .Where(x => !x.EsSubtramite)
                    .ToList();

                principales = AplicarFiltro(principales, filtro);

                var modelos = principales
                    .SelectMany(e => e.EmbarqueModelos ?? new List<EmbarqueModelo>())
                    .Select(em => ObtenerNombreModelo(em))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Reporte Embarques");

                int fila = 1;
                int totalColumnas = 7 + modelos.Count;

                ws.Cell(fila, 1).Value = "REPORTE DE EMBARQUES";
                ws.Range(fila, 1, fila, totalColumnas).Merge();
                ws.Cell(fila, 1).Style.Font.Bold = true;
                ws.Cell(fila, 1).Style.Font.FontSize = 14;
                ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                fila++;

                string nombreMarca = "Todas las marcas";
                if (marcaId.HasValue)
                {
                    nombreMarca = _context.Marcas
                        .Where(m => m.Id == marcaId.Value)
                        .Select(m => m.Nombre)
                        .FirstOrDefault() ?? "Todas las marcas";
                }

                ws.Cell(fila, 1).Value = "Marca:";
                ws.Cell(fila, 1).Style.Font.Bold = true;
                ws.Cell(fila, 2).Value = nombreMarca;

                ws.Cell(fila, 4).Value = "Filtro:";
                ws.Cell(fila, 4).Style.Font.Bold = true;
                ws.Cell(fila, 5).Value = string.IsNullOrWhiteSpace(filtro) ? "todos" : filtro;
                fila++;

                ws.Cell(fila, 1).Value = "Búsqueda:";
                ws.Cell(fila, 1).Style.Font.Bold = true;
                ws.Cell(fila, 2).Value = string.IsNullOrWhiteSpace(q) ? "(sin búsqueda)" : q;
                fila++;

                ws.Cell(fila, 1).Value = "Fecha:";
                ws.Cell(fila, 1).Style.Font.Bold = true;
                ws.Cell(fila, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                fila += 2;

                int filaEncabezado = fila;

                ws.Cell(fila, 1).Value = "Shipment";
                ws.Cell(fila, 2).Value = "Puerto destino";
                ws.Cell(fila, 3).Value = "INCOTERM";
                ws.Cell(fila, 4).Value = "Naviera";
                ws.Cell(fila, 5).Value = "ETD";
                ws.Cell(fila, 6).Value = "ETA";
                ws.Cell(fila, 7).Value = "QTY";

                int colModeloInicio = 8;

                for (int i = 0; i < modelos.Count; i++)
                {
                    ws.Cell(fila, colModeloInicio + i).Value = modelos[i];
                }

                var rangoEncabezadoFijo = ws.Range(fila, 1, fila, 7);
                rangoEncabezadoFijo.Style.Font.Bold = true;
                rangoEncabezadoFijo.Style.Font.FontColor = XLColor.White;
                rangoEncabezadoFijo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rangoEncabezadoFijo.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                rangoEncabezadoFijo.Style.Fill.BackgroundColor = XLColor.FromHtml("#0B2C6F");
                rangoEncabezadoFijo.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rangoEncabezadoFijo.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                if (modelos.Count > 0)
                {
                    var rangoEncabezadoModelos = ws.Range(fila, colModeloInicio, fila, colModeloInicio + modelos.Count - 1);
                    rangoEncabezadoModelos.Style.Font.Bold = true;
                    rangoEncabezadoModelos.Style.Font.FontColor = XLColor.White;
                    rangoEncabezadoModelos.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    rangoEncabezadoModelos.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    rangoEncabezadoModelos.Style.Fill.BackgroundColor = XLColor.FromHtml("#1574B8");
                    rangoEncabezadoModelos.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rangoEncabezadoModelos.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                }

                fila++;

                int filaInicioDatos = fila;

                foreach (var item in principales)
                {
                    var respuestas = item.Respuestas?.ToList() ?? new List<Respuesta>();

                    var shipment = GetValor(respuestas, "SHIPMENT");
                    var puertoDestino = GetValor(respuestas, "DESTINATION");
                    var incoterm = GetValor(respuestas, "INCOTERM");
                    if (string.IsNullOrWhiteSpace(incoterm))
                        incoterm = GetValor(respuestas, "INCOTERMS");

                    var naviera = GetValor(respuestas, "SHIPPING");
                    var etd = GetFecha(respuestas, "DEPARTURE");
                    var eta = GetFecha(respuestas, "ETA");

                    var qtyTotal = item.EmbarqueModelos?.Sum(x => x.Cantidad) ?? 0;

                    ws.Cell(fila, 1).Value = shipment ?? "";
                    ws.Cell(fila, 2).Value = puertoDestino ?? "";
                    ws.Cell(fila, 3).Value = incoterm ?? "";
                    ws.Cell(fila, 4).Value = naviera ?? "";

                    if (etd.HasValue)
                    {
                        ws.Cell(fila, 5).Value = etd.Value;
                        ws.Cell(fila, 5).Style.DateFormat.Format = "dd-MMM";
                    }

                    if (eta.HasValue)
                    {
                        ws.Cell(fila, 6).Value = eta.Value;
                        ws.Cell(fila, 6).Style.DateFormat.Format = "dd-MMM";
                    }

                    ws.Cell(fila, 7).Value = qtyTotal;

                    var cantidadesPorModelo = (item.EmbarqueModelos ?? new List<EmbarqueModelo>())
                        .GroupBy(x => ObtenerNombreModelo(x))
                        .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

                    for (int i = 0; i < modelos.Count; i++)
                    {
                        var nombreModelo = modelos[i];
                        var col = colModeloInicio + i;

                        ws.Cell(fila, col).Value = cantidadesPorModelo.ContainsKey(nombreModelo)
                            ? cantidadesPorModelo[nombreModelo]
                            : 0;
                    }

                    fila++;
                }

                ws.Cell(fila, 7).Value = principales.Sum(x => x.EmbarqueModelos?.Sum(m => m.Cantidad) ?? 0);
                ws.Cell(fila, 7).Style.Font.Bold = true;

                for (int i = 0; i < modelos.Count; i++)
                {
                    var nombreModelo = modelos[i];
                    var col = colModeloInicio + i;

                    int totalModelo = principales.Sum(e =>
                        (e.EmbarqueModelos ?? new List<EmbarqueModelo>())
                        .Where(x => ObtenerNombreModelo(x) == nombreModelo)
                        .Sum(x => x.Cantidad)
                    );

                    ws.Cell(fila, col).Value = totalModelo;
                    ws.Cell(fila, col).Style.Font.Bold = true;
                }

                var rangoTotal = ws.Range(fila, 7, fila, totalColumnas);
                rangoTotal.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
                rangoTotal.Style.Font.Bold = true;
                rangoTotal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rangoTotal.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rangoTotal.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                if (fila > filaEncabezado)
                {
                    var rangoTabla = ws.Range(filaEncabezado, 1, fila, totalColumnas);
                    rangoTabla.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    rangoTabla.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    rangoTabla.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }

                if (fila > filaInicioDatos)
                {
                    var rangoETD = ws.Range(filaInicioDatos, 5, fila - 1, 5);
                    var rangoETA = ws.Range(filaInicioDatos, 6, fila - 1, 6);

                    rangoETD.Style.Fill.BackgroundColor = XLColor.FromHtml("#CFEF8A");
                    rangoETA.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF16A");
                }

                ws.Columns().AdjustToContents();
                ws.Column(1).Width = 24;
                ws.Column(2).Width = 14;
                ws.Column(3).Width = 12;
                ws.Column(4).Width = 14;
                ws.Column(5).Width = 10;
                ws.Column(6).Width = 10;
                ws.Column(7).Width = 8;

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var contenido = stream.ToArray();

                string archivo = $"Reporte_Embarques_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(
                    contenido,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    archivo
                );
            }
            catch (Exception ex)
            {
                return Content("ERROR EN EXPORTAR EXCEL: " + ex.Message + " | " + ex.InnerException?.Message);
            }
        }

        private List<Embarque> AplicarFiltro(List<Embarque> items, string filtro)
        {
            filtro = (filtro ?? "todos").Trim().ToLower();

            return filtro switch
            {
                "transito" => items.Where(x => EsEnTransito(x)).ToList(),
                "entregado" => items.Where(x => EsEntregado(x)).ToList(),
                "retrasado" => items.Where(x => EsRetrasado(x)).ToList(),
                _ => items
            };
        }

        private bool EsEntregado(Embarque item)
        {
            var respuestas = item.Respuestas?.ToList() ?? new List<Respuesta>();
            var status = GetValor(respuestas, "STATUS")?.Trim();

            return string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Entregado", StringComparison.OrdinalIgnoreCase);
        }

        private bool EsEnTransito(Embarque item)
        {
            return !EsEntregado(item);
        }

        private bool EsRetrasado(Embarque item)
        {
            var respuestas = item.Respuestas?.ToList() ?? new List<Respuesta>();
            var status = GetValor(respuestas, "STATUS");
            var departure = GetFecha(respuestas, "DEPARTURE");

            var camposObligatorios = new List<string>
            {
                "ETA",
                "TRÁNSITO",
                "HOMOLOGATION",
                "CERTIFICADO 017 INEN",
                "AFIP",
                "INVOICES",
                "BLs",
                "BL",
                "COs",
                "APLICACIONES DE SEGURO",
                "LICENCIA INEN",
                "PACKING",
                "SUBCATEGOR",
                "NOTIFICACIÓN SRI",
                "DESTINO ADUANERO",
                "TIPO DE PAGO",
                "DIAS LIBRES",
                "MRN",
                "PRE-LIQUIDACIÓN",
                "EMISION BL",
                "CAS",
                "PAGO FLETE",
                "ORIGINAL",
                "ESTADO DEL BUQUE"
            };

            if (string.Equals(status?.Trim(), "Closed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status?.Trim(), "Entregado", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!departure.HasValue || departure.Value.Date.AddDays(1) >= DateTime.Now.Date)
            {
                return false;
            }

            foreach (var campo in camposObligatorios)
            {
                var valor = GetValor(respuestas, campo);
                if (string.IsNullOrWhiteSpace(valor))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetValor(List<Respuesta> respuestas, string nombre)
        {
            return respuestas.FirstOrDefault(r =>
                r.Pregunta != null &&
                r.Pregunta.Nombre != null &&
                r.Pregunta.Nombre.ToUpper().Contains(nombre.ToUpper())
            )?.Valor ?? "";
        }

        private DateTime? GetFecha(List<Respuesta> respuestas, string nombre)
        {
            var val = GetValor(respuestas, nombre);
            return DateTime.TryParse(val, out DateTime d) ? d : (DateTime?)null;
        }

        private string ObtenerNombreModelo(EmbarqueModelo em)
        {
            if (em == null)
                return "";

            if (em.Modelo != null && !string.IsNullOrWhiteSpace(em.Modelo.Nombre))
                return em.Modelo.Nombre.Trim();

            var propNombreModelo = em.GetType().GetProperty("NombreModelo");
            if (propNombreModelo != null)
                return propNombreModelo.GetValue(em)?.ToString()?.Trim() ?? "";

            var propModeloNombre = em.GetType().GetProperty("ModeloNombre");
            if (propModeloNombre != null)
                return propModeloNombre.GetValue(em)?.ToString()?.Trim() ?? "";

            return "";
        }
    }
}