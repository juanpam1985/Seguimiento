using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;
using Seguimiento.Models.ViewModels;

namespace Seguimiento.Controllers
{
    public class PackingListsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PackingListsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ======================================================
        // ABRIR / CREAR PACKING LIST DESDE UN SUBTRÁMITE
        // ======================================================
        public async Task<IActionResult> PorSubtramite(int subtramiteId)
        {
            var vm = await ConstruirPagina(subtramiteId);

            if (vm == null)
                return NotFound();

            return View("DetallePacking", vm);
        }

        // ======================================================
        // CREAR DETALLE
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearDetalle(PackingListDetallePageViewModel vm)
        {
            if (vm == null || vm.NuevoDetalle == null)
                return BadRequest();

            var packing = await _context.PackingLists
                .Include(p => p.Detalles.Where(d => d.Activo))
                .FirstOrDefaultAsync(p => p.Id == vm.NuevoDetalle.PackingListId && p.Activo);

            if (packing == null)
                return NotFound();

            ValidarDetalle(vm.NuevoDetalle, null);

            if (!ModelState.IsValid)
            {
                ViewBag.ErroresDebug = ObtenerErroresModelState();

                var paginaError = await ConstruirPagina(packing.SubtramiteId);
                if (paginaError == null)
                    return NotFound();

                paginaError.NuevoDetalle = vm.NuevoDetalle;
                return View("DetallePacking", paginaError);
            }

            await CompletarDatosDesdeCatalogos(vm.NuevoDetalle);

            var siguienteLinea = 1;
            if (packing.Detalles != null && packing.Detalles.Any())
                siguienteLinea = packing.Detalles.Max(d => d.Linea) + 1;

            vm.NuevoDetalle.Linea = siguienteLinea;
            vm.NuevoDetalle.Activo = true;
            vm.NuevoDetalle.FechaCreacion = DateTime.Now;

            try
            {
                _context.PackingListDetalles.Add(vm.NuevoDetalle);
                await _context.SaveChangesAsync();

                TempData["OK"] = "Línea de packing guardada correctamente.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId = packing.SubtramiteId });
            }
            catch (DbUpdateException ex)
            {
                MapearErroresIndices(ex, vm.NuevoDetalle);

                ViewBag.ErroresDebug = ObtenerErroresModelState();

                var paginaError = await ConstruirPagina(packing.SubtramiteId);
                if (paginaError == null)
                    return NotFound();

                paginaError.NuevoDetalle = vm.NuevoDetalle;
                return View("DetallePacking", paginaError);
            }
        }

        // ======================================================
        // IMPORTAR EXCEL
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarExcel(int subtramiteId, IFormFile archivoExcel)
        {
            if (archivoExcel == null || archivoExcel.Length == 0)
            {
                TempData["Error"] = "Debes seleccionar un archivo Excel.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }

            var extension = Path.GetExtension(archivoExcel.FileName)?.ToLowerInvariant();
            if (extension != ".xlsx")
            {
                TempData["Error"] = "Solo se permite importar archivos .xlsx";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }

            var packing = await _context.PackingLists
                .Include(p => p.Detalles.Where(d => d.Activo))
                .FirstOrDefaultAsync(p => p.SubtramiteId == subtramiteId && p.Activo);

            if (packing == null)
            {
                TempData["Error"] = "No se encontró el packing list del subtrámite.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }

            using var stream = new MemoryStream();
            await archivoExcel.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheets.FirstOrDefault();

            if (ws == null)
            {
                TempData["Error"] = "El archivo no contiene hojas para importar.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }

            int filaHeader = BuscarFilaHeader(ws);
            if (filaHeader == 0)
            {
                TempData["Error"] = "No se encontró la fila de encabezados esperada en el Excel.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }

            var headersEsperados = new[]
            {
                "ITEM",
                "REQUEST NO.",
                "ORIGIN PORT",
                "DESTINATIN PORT",
                "SHIP",
                "FECHA EMBARQUE",
                "FECHA ARRIBO",
                "INVOICE NUMBER",
                "B/L",
                "VIN NO.",
                "MODEL",
                "MODEL (SRI)",
                "MODEL (HOMOLOGACIÓN)",
                "ENGINE CODE",
                "ENGINE NUMBER",
                "COLOR",
                "COLOR NAME",
                "COLOR LOCAL",
                "MODEL YEAR",
                "NET WEIGHT (KGS)",
                "CBM",
                "PRECIO",
                "FLETE",
                "SEGURO",
                "INCOTERM"
            };

            for (int i = 0; i < headersEsperados.Length; i++)
            {
                var valorHeaderOriginal = ws.Cell(filaHeader, i + 1).GetString();
                var valorHeader = NormalizarHeader(valorHeaderOriginal);

                if (valorHeader != headersEsperados[i])
                {
                    TempData["Error"] = $"La columna {i + 1} no coincide. Se esperaba '{headersEsperados[i]}' y se encontró '{valorHeaderOriginal}'.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }
            }

            int ultimaFila = ws.LastRowUsed()?.RowNumber() ?? filaHeader;
            if (ultimaFila <= filaHeader)
            {
                TempData["Error"] = "El archivo no contiene filas de detalle para importar.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }

            var filasPreparadas = new List<(int FilaExcel, PackingListDetalle Detalle, string RequestNo, DateTime? FechaEmbarque, DateTime? FechaArribo)>();
            var vinsArchivo = new HashSet<string>();
            var enginesArchivo = new HashSet<string>();

            int siguienteLinea = (packing.Detalles != null && packing.Detalles.Any())
                ? packing.Detalles.Max(d => d.Linea) + 1
                : 1;

            for (int fila = filaHeader + 1; fila <= ultimaFila; fila++)
            {
                bool filaVacia = ws.Row(fila).Cells(1, 25).All(c => string.IsNullOrWhiteSpace(c.GetString()));
                if (filaVacia)
                    continue;

                string requestNo = ws.Cell(fila, 2).GetString().Trim();
                string originPort = ws.Cell(fila, 3).GetString().Trim();
                string destinationPort = ws.Cell(fila, 4).GetString().Trim();
                string ship = ws.Cell(fila, 5).GetString().Trim();
                DateTime? fechaEmbarque = LeerFechaExcel(ws.Cell(fila, 6));
                DateTime? fechaArribo = LeerFechaExcel(ws.Cell(fila, 7));
                string invoiceNumber = ws.Cell(fila, 8).GetString().Trim();
                string bl = ws.Cell(fila, 9).GetString().Trim();
                string vinNo = ws.Cell(fila, 10).GetString().Trim();

                string modelPacking = ws.Cell(fila, 11).GetString().Trim();
                string modelSRI = "";
                string modelHomologacion = "";

                string engineCode = ws.Cell(fila, 14).GetString().Trim();
                string engineNumber = ws.Cell(fila, 15).GetString().Trim();

                string colorCodigo = ws.Cell(fila, 16).GetString().Trim();
                string colorName = "";
                string colorLocal = "";

                string modelYear = ws.Cell(fila, 19).GetString().Trim();
                decimal? netWeightKg = LeerDecimalExcel(ws.Cell(fila, 20));
                decimal? cbm = LeerDecimalExcel(ws.Cell(fila, 21));
                decimal? precio = LeerDecimalExcel(ws.Cell(fila, 22));
                decimal? flete = LeerDecimalExcel(ws.Cell(fila, 23));
                decimal? seguro = LeerDecimalExcel(ws.Cell(fila, 24));
                string incoterm = ws.Cell(fila, 25).GetString().Trim();

                if (string.IsNullOrWhiteSpace(vinNo))
                {
                    TempData["Error"] = $"La fila {fila} no tiene VIN No.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                if (string.IsNullOrWhiteSpace(engineNumber))
                {
                    TempData["Error"] = $"La fila {fila} no tiene Engine Number.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                if (string.IsNullOrWhiteSpace(modelPacking))
                {
                    TempData["Error"] = $"La fila {fila} no tiene Model.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                if (string.IsNullOrWhiteSpace(colorCodigo))
                {
                    TempData["Error"] = $"La fila {fila} no tiene Color.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                var vinKey = vinNo.Trim().ToUpper();
                var engineKey = engineNumber.Trim().ToUpper();

                if (!vinsArchivo.Add(vinKey))
                {
                    TempData["Error"] = $"El VIN '{vinNo}' está repetido dentro del archivo Excel, fila {fila}.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                if (!enginesArchivo.Add(engineKey))
                {
                    TempData["Error"] = $"El Engine Number '{engineNumber}' está repetido dentro del archivo Excel, fila {fila}.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                var vinSistema = await _context.PackingListDetalles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Activo && d.VinNo != null && d.VinNo.Trim().ToUpper() == vinKey);

                if (vinSistema != null)
                {
                    TempData["Error"] = $"El VIN '{vinNo}' de la fila {fila} ya existe en la línea {vinSistema.Linea} del packing #{vinSistema.PackingListId}.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                var engineSistema = await _context.PackingListDetalles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Activo && d.EngineNumber != null && d.EngineNumber.Trim().ToUpper() == engineKey);

                if (engineSistema != null)
                {
                    TempData["Error"] = $"El Engine Number '{engineNumber}' de la fila {fila} ya existe en la línea {engineSistema.Linea} del packing #{engineSistema.PackingListId}.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                int? modeloId = null;
                int? colorId = null;

                var modelosPermitidos = await ObtenerModelosPorEmpresaDelSubtramite(subtramiteId);
                var modelo = modelosPermitidos.FirstOrDefault(m =>
                    m.ModelPacking != null &&
                    m.ModelPacking.Trim().ToUpper() == modelPacking.Trim().ToUpper());

                if (modelo != null)
                {
                    modeloId = modelo.Id;
                    modelSRI = modelo.ModelSRI ?? "";
                    modelHomologacion = modelo.ModelHomologacion ?? "";
                }
                else
                {
                    TempData["Error"] = $"No se encontró el modelo '{modelPacking}' en la fila {fila}, o no pertenece a una marca asignada a esta empresa.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                var color = await _context.Colores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.Activo &&
                        c.Codigo != null &&
                        c.Codigo.Trim().ToUpper() == colorCodigo.Trim().ToUpper());

                if (color != null)
                {
                    colorId = color.Id;
                    colorCodigo = color.Codigo ?? "";
                    colorName = color.Nombre ?? "";
                    colorLocal = color.NombreLocal ?? "";
                }
                else
                {
                    TempData["Error"] = $"No se encontró el color '{colorCodigo}' en la fila {fila}. Debe existir en el catálogo de Colores.";
                    return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
                }

                var detalle = new PackingListDetalle
                {
                    PackingListId = packing.Id,
                    Linea = siguienteLinea,
                    OriginPort = originPort,
                    DestinationPort = destinationPort,
                    Ship = ship,
                    InvoiceNumber = invoiceNumber,
                    BL = bl,
                    VinNo = vinNo,
                    ModeloId = modeloId,
                    ModelPacking = modelPacking,
                    ModelSRI = modelSRI,
                    ModelHomologacion = modelHomologacion,
                    EngineCode = engineCode,
                    EngineNumber = engineNumber,
                    ColorId = colorId,
                    ColorCodigo = colorCodigo,
                    ColorName = colorName,
                    ColorLocal = colorLocal,
                    ModelYear = modelYear,
                    NetWeightKg = netWeightKg,
                    CBM = cbm,
                    Precio = precio,
                    Flete = flete,
                    Seguro = seguro,
                    Incoterm = incoterm,
                    Activo = true,
                    FechaCreacion = DateTime.Now
                };

                filasPreparadas.Add((fila, detalle, requestNo, fechaEmbarque, fechaArribo));
                siguienteLinea++;
            }

            if (!filasPreparadas.Any())
            {
                TempData["Error"] = "No se encontraron filas válidas para importar.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var primera = filasPreparadas.First();

                if (!string.IsNullOrWhiteSpace(primera.RequestNo))
                    packing.RequestNo = primera.RequestNo;

                if (primera.FechaEmbarque.HasValue)
                    packing.FechaEmbarque = primera.FechaEmbarque;

                if (primera.FechaArribo.HasValue)
                    packing.FechaArribo = primera.FechaArribo;

                foreach (var item in filasPreparadas)
                {
                    _context.PackingListDetalles.Add(item.Detalle);
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["OK"] = $"Importación completada. Se cargaron {filasPreparadas.Count} líneas.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();

                var mensaje = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = "Ocurrió un error al importar el Excel: " + mensaje;
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId });
            }
        }

        // ======================================================
        // EDITAR DETALLE GET
        // ======================================================
        public async Task<IActionResult> EditarDetalle(int id)
        {
            var detalle = await _context.PackingListDetalles
                .Include(d => d.PackingList)
                .FirstOrDefaultAsync(d => d.Id == id && d.Activo);

            if (detalle == null)
                return NotFound();

            var vm = new PackingListDetallePageViewModel
            {
                Packing = detalle.PackingList,
                NuevoDetalle = detalle,
                Modelos = await ObtenerModelosPorEmpresaDelSubtramite(detalle.PackingList.SubtramiteId),
                Colores = await _context.Colores
                    .AsNoTracking()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Codigo)
                    .ToListAsync(),
                SubtramiteId = detalle.PackingList.SubtramiteId,
                SubtramiteTitulo = detalle.PackingList.RequestNo
            };

            return View("EditarDetalle", vm);
        }

        // ======================================================
        // EDITAR DETALLE POST
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarDetalle(PackingListDetallePageViewModel vm)
        {
            if (vm == null || vm.NuevoDetalle == null)
                return BadRequest();

            var detalleDb = await _context.PackingListDetalles
                .Include(d => d.PackingList)
                .FirstOrDefaultAsync(d => d.Id == vm.NuevoDetalle.Id && d.Activo);

            if (detalleDb == null)
                return NotFound();

            ValidarDetalle(vm.NuevoDetalle, vm.NuevoDetalle.Id);

            if (!ModelState.IsValid)
            {
                ViewBag.ErroresDebug = ObtenerErroresModelState();

                vm.Packing = detalleDb.PackingList;
                vm.SubtramiteId = detalleDb.PackingList.SubtramiteId;
                vm.SubtramiteTitulo = detalleDb.PackingList.RequestNo;
                vm.Modelos = await ObtenerModelosPorEmpresaDelSubtramite(detalleDb.PackingList.SubtramiteId);
                vm.Colores = await _context.Colores
                    .AsNoTracking()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Codigo)
                    .ToListAsync();

                return View("EditarDetalle", vm);
            }

            await CompletarDatosDesdeCatalogos(vm.NuevoDetalle);

            detalleDb.OriginPort = vm.NuevoDetalle.OriginPort;
            detalleDb.DestinationPort = vm.NuevoDetalle.DestinationPort;
            detalleDb.Ship = vm.NuevoDetalle.Ship;
            detalleDb.InvoiceNumber = vm.NuevoDetalle.InvoiceNumber;
            detalleDb.BL = vm.NuevoDetalle.BL;
            detalleDb.VinNo = vm.NuevoDetalle.VinNo;
            detalleDb.ModeloId = vm.NuevoDetalle.ModeloId;
            detalleDb.ModelPacking = vm.NuevoDetalle.ModelPacking;
            detalleDb.ModelSRI = vm.NuevoDetalle.ModelSRI;
            detalleDb.ModelHomologacion = vm.NuevoDetalle.ModelHomologacion;
            detalleDb.EngineCode = vm.NuevoDetalle.EngineCode;
            detalleDb.EngineNumber = vm.NuevoDetalle.EngineNumber;
            detalleDb.ColorId = vm.NuevoDetalle.ColorId;
            detalleDb.ColorCodigo = vm.NuevoDetalle.ColorCodigo;
            detalleDb.ColorName = vm.NuevoDetalle.ColorName;
            detalleDb.ColorLocal = vm.NuevoDetalle.ColorLocal;
            detalleDb.ModelYear = vm.NuevoDetalle.ModelYear;
            detalleDb.NetWeightKg = vm.NuevoDetalle.NetWeightKg;
            detalleDb.CBM = vm.NuevoDetalle.CBM;
            detalleDb.Precio = vm.NuevoDetalle.Precio;
            detalleDb.Flete = vm.NuevoDetalle.Flete;
            detalleDb.Seguro = vm.NuevoDetalle.Seguro;
            detalleDb.Incoterm = vm.NuevoDetalle.Incoterm;

            try
            {
                await _context.SaveChangesAsync();

                TempData["OK"] = "Línea actualizada correctamente.";
                return RedirectToAction(nameof(PorSubtramite), new { subtramiteId = detalleDb.PackingList.SubtramiteId });
            }
            catch (DbUpdateException ex)
            {
                MapearErroresIndices(ex, vm.NuevoDetalle);

                ViewBag.ErroresDebug = ObtenerErroresModelState();

                vm.Packing = detalleDb.PackingList;
                vm.SubtramiteId = detalleDb.PackingList.SubtramiteId;
                vm.SubtramiteTitulo = detalleDb.PackingList.RequestNo;
                vm.Modelos = await ObtenerModelosPorEmpresaDelSubtramite(detalleDb.PackingList.SubtramiteId);
                vm.Colores = await _context.Colores
                    .AsNoTracking()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Codigo)
                    .ToListAsync();

                return View("EditarDetalle", vm);
            }
        }

        // ======================================================
        // ELIMINAR DETALLE
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarDetalle(int id)
        {
            var detalle = await _context.PackingListDetalles
                .Include(d => d.PackingList)
                .FirstOrDefaultAsync(d => d.Id == id && d.Activo);

            if (detalle == null)
                return NotFound();

            detalle.Activo = false;
            await _context.SaveChangesAsync();

            TempData["OK"] = "Línea eliminada correctamente.";
            return RedirectToAction(nameof(PorSubtramite), new { subtramiteId = detalle.PackingList.SubtramiteId });
        }

        // ======================================================
        // EXPORTAR EXCEL
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> ExportarExcel(int subtramiteId)
        {
            var subtramite = await _context.Embarques
                .Include(e => e.EmbarquePadre)
                    .ThenInclude(p => p.Respuestas)
                        .ThenInclude(r => r.Pregunta)
                .Include(e => e.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .FirstOrDefaultAsync(e => e.Id == subtramiteId && e.EsSubtramite);

            if (subtramite == null)
                return NotFound();

            var packing = await _context.PackingLists
                .Include(p => p.Detalles.Where(d => d.Activo))
                .FirstOrDefaultAsync(p => p.SubtramiteId == subtramiteId && p.Activo);

            if (packing == null)
                return NotFound();

            var shipmentPadre = "";
            if (subtramite.EmbarquePadre != null)
            {
                var respuestasPadre = subtramite.EmbarquePadre.Respuestas?.ToList() ?? new List<Respuesta>();
                shipmentPadre = GetCampoTexto(respuestasPadre, "SHIPMENT");
            }

            var shipmentSub = packing.RequestNo ?? "";
            var totalItems = packing.Detalles?.Count(d => d.Activo) ?? 0;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Packing List");

            ws.Cell("A1").Value = "PACKING LIST";
            ws.Range("A1:Y1").Merge();
            ws.Range("A1:Y1").Style.Font.Bold = true;
            ws.Range("A1:Y1").Style.Font.FontSize = 16;
            ws.Range("A1:Y1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("A1:Y1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Range("A1:Y1").Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");

            ws.Cell("A2").Value = "Shipment Padre:";
            ws.Cell("B2").Value = shipmentPadre;
            ws.Cell("H2").Value = "Shipment Subtrámite:";
            ws.Cell("I2").Value = shipmentSub;
            ws.Cell("P2").Value = "Total Ítems:";
            ws.Cell("Q2").Value = totalItems;

            ws.Range("A2:Q2").Style.Font.Bold = true;
            ws.Range("A2:Q2").Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6FA");

            int filaHeader = 4;

            var headers = new[]
            {
                "Item",
                "Request No.",
                "Origin Port",
                "Destinatin Port",
                "Ship",
                "Fecha Embarque",
                "Fecha Arribo",
                "Invoice Number",
                "B/L",
                "VIN No.",
                "Model",
                "Model (SRI)",
                "Model (HOMOLOGACIÓN)",
                "Engine Code",
                "Engine Number",
                "Color",
                "Color Name",
                "Color Local",
                "Model Year",
                "Net Weight (KGs)",
                "CBM",
                "Precio",
                "Flete",
                "Seguro",
                "Incoterm"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(filaHeader, i + 1).Value = headers[i];
            }

            var rangoHeader = ws.Range(filaHeader, 1, filaHeader, headers.Length);
            rangoHeader.Style.Font.Bold = true;
            rangoHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            rangoHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rangoHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            rangoHeader.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rangoHeader.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int fila = filaHeader + 1;
            int item = 1;

            foreach (var d in packing.Detalles.Where(x => x.Activo).OrderBy(x => x.Linea))
            {
                ws.Cell(fila, 1).Value = item;
                ws.Cell(fila, 2).Value = packing.RequestNo;
                ws.Cell(fila, 3).Value = d.OriginPort;
                ws.Cell(fila, 4).Value = d.DestinationPort;
                ws.Cell(fila, 5).Value = d.Ship;
                ws.Cell(fila, 6).Value = packing.FechaEmbarque;
                ws.Cell(fila, 7).Value = packing.FechaArribo;
                ws.Cell(fila, 8).Value = d.InvoiceNumber;
                ws.Cell(fila, 9).Value = d.BL;
                ws.Cell(fila, 10).Value = d.VinNo;
                ws.Cell(fila, 11).Value = d.ModelPacking;
                ws.Cell(fila, 12).Value = d.ModelSRI;
                ws.Cell(fila, 13).Value = d.ModelHomologacion;
                ws.Cell(fila, 14).Value = d.EngineCode;
                ws.Cell(fila, 15).Value = d.EngineNumber;
                ws.Cell(fila, 16).Value = d.ColorCodigo;
                ws.Cell(fila, 17).Value = d.ColorName;
                ws.Cell(fila, 18).Value = d.ColorLocal;
                ws.Cell(fila, 19).Value = d.ModelYear;
                ws.Cell(fila, 20).Value = d.NetWeightKg;
                ws.Cell(fila, 21).Value = d.CBM;
                ws.Cell(fila, 22).Value = d.Precio;
                ws.Cell(fila, 23).Value = d.Flete;
                ws.Cell(fila, 24).Value = d.Seguro;
                ws.Cell(fila, 25).Value = d.Incoterm;

                fila++;
                item++;
            }

            if (fila > filaHeader + 1)
            {
                var rangoDatos = ws.Range(filaHeader + 1, 1, fila - 1, 25);
                rangoDatos.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rangoDatos.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rangoDatos.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            ws.Column(6).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Column(7).Style.DateFormat.Format = "dd/MM/yyyy";

            ws.Column(20).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(21).Style.NumberFormat.Format = "#,##0.000";
            ws.Column(22).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(23).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(24).Style.NumberFormat.Format = "#,##0.00";

            ws.Columns().AdjustToContents();

            ws.SheetView.FreezeRows(filaHeader);

            var nombreArchivo = $"PackingList_{shipmentSub}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var contenido = stream.ToArray();

            return File(
                contenido,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                nombreArchivo
            );
        }

        // ======================================================
        // EXCEL CONSOLIDADO
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> DescargarConsolidadoPorEmbarque(int embarquePadreId)
        {
            var embarquePadre = await _context.Embarques
                .Include(e => e.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .FirstOrDefaultAsync(e => e.Id == embarquePadreId && !e.EsSubtramite);

            if (embarquePadre == null)
                return NotFound();

            var subtramites = await _context.Embarques
                .Include(e => e.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .Where(e => e.EsSubtramite && e.EmbarquePadreId == embarquePadreId)
                .OrderBy(e => e.OrdenSubtramite ?? int.MaxValue)
                .ThenBy(e => e.Id)
                .ToListAsync();

            if (!subtramites.Any())
            {
                TempData["Error"] = "Este embarque no tiene subtrámites para descargar.";
                return RedirectToAction("Dashboard", "Embarques");
            }

            var subtramiteIds = subtramites.Select(s => s.Id).ToList();

            var packings = await _context.PackingLists
                .Include(p => p.Detalles.Where(d => d.Activo))
                .Where(p => p.Activo && subtramiteIds.Contains(p.SubtramiteId))
                .ToListAsync();

            if (!packings.Any())
            {
                TempData["Error"] = "Este embarque no tiene packing list en sus subtrámites.";
                return RedirectToAction("Dashboard", "Embarques");
            }

            var shipmentPadre = GetCampoTexto(embarquePadre.Respuestas?.ToList() ?? new List<Respuesta>(), "SHIPMENT");
            if (string.IsNullOrWhiteSpace(shipmentPadre))
                shipmentPadre = $"EMBARQUE_{embarquePadre.Id}";

            var filasConsolidadas = new List<(PackingList Packing, PackingListDetalle Detalle)>();

            foreach (var sub in subtramites)
            {
                var packing = packings.FirstOrDefault(p => p.SubtramiteId == sub.Id);
                if (packing == null || packing.Detalles == null || !packing.Detalles.Any(d => d.Activo))
                    continue;

                foreach (var detalle in packing.Detalles.Where(d => d.Activo).OrderBy(d => d.Linea))
                {
                    filasConsolidadas.Add((packing, detalle));
                }
            }

            if (!filasConsolidadas.Any())
            {
                TempData["Error"] = "Los packing list encontrados no tienen líneas activas.";
                return RedirectToAction("Dashboard", "Embarques");
            }

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Packing List");

            ws.Cell("A1").Value = "PACKING LIST";
            ws.Range("A1:Y1").Merge();
            ws.Range("A1:Y1").Style.Font.Bold = true;
            ws.Range("A1:Y1").Style.Font.FontSize = 16;
            ws.Range("A1:Y1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("A1:Y1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Range("A1:Y1").Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");

            ws.Cell("A2").Value = "Shipment Padre:";
            ws.Cell("B2").Value = shipmentPadre;
            ws.Cell("H2").Value = "Shipment Subtrámite:";
            ws.Cell("I2").Value = "CONSOLIDADO";
            ws.Cell("P2").Value = "Total Ítems:";
            ws.Cell("Q2").Value = filasConsolidadas.Count;

            ws.Range("A2:Q2").Style.Font.Bold = true;
            ws.Range("A2:Q2").Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6FA");

            int filaHeader = 4;

            var headers = new[]
            {
                "Item",
                "Request No.",
                "Origin Port",
                "Destinatin Port",
                "Ship",
                "Fecha Embarque",
                "Fecha Arribo",
                "Invoice Number",
                "B/L",
                "VIN No.",
                "Model",
                "Model (SRI)",
                "Model (HOMOLOGACIÓN)",
                "Engine Code",
                "Engine Number",
                "Color",
                "Color Name",
                "Color Local",
                "Model Year",
                "Net Weight (KGs)",
                "CBM",
                "Precio",
                "Flete",
                "Seguro",
                "Incoterm"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(filaHeader, i + 1).Value = headers[i];
            }

            var rangoHeader = ws.Range(filaHeader, 1, filaHeader, headers.Length);
            rangoHeader.Style.Font.Bold = true;
            rangoHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            rangoHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rangoHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            rangoHeader.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rangoHeader.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int fila = filaHeader + 1;
            int item = 1;

            foreach (var filaData in filasConsolidadas)
            {
                var packing = filaData.Packing;
                var d = filaData.Detalle;

                ws.Cell(fila, 1).Value = item;
                ws.Cell(fila, 2).Value = packing.RequestNo;
                ws.Cell(fila, 3).Value = d.OriginPort;
                ws.Cell(fila, 4).Value = d.DestinationPort;
                ws.Cell(fila, 5).Value = d.Ship;
                ws.Cell(fila, 6).Value = packing.FechaEmbarque;
                ws.Cell(fila, 7).Value = packing.FechaArribo;
                ws.Cell(fila, 8).Value = d.InvoiceNumber;
                ws.Cell(fila, 9).Value = d.BL;
                ws.Cell(fila, 10).Value = d.VinNo;
                ws.Cell(fila, 11).Value = d.ModelPacking;
                ws.Cell(fila, 12).Value = d.ModelSRI;
                ws.Cell(fila, 13).Value = d.ModelHomologacion;
                ws.Cell(fila, 14).Value = d.EngineCode;
                ws.Cell(fila, 15).Value = d.EngineNumber;
                ws.Cell(fila, 16).Value = d.ColorCodigo;
                ws.Cell(fila, 17).Value = d.ColorName;
                ws.Cell(fila, 18).Value = d.ColorLocal;
                ws.Cell(fila, 19).Value = d.ModelYear;
                ws.Cell(fila, 20).Value = d.NetWeightKg;
                ws.Cell(fila, 21).Value = d.CBM;
                ws.Cell(fila, 22).Value = d.Precio;
                ws.Cell(fila, 23).Value = d.Flete;
                ws.Cell(fila, 24).Value = d.Seguro;
                ws.Cell(fila, 25).Value = d.Incoterm;

                fila++;
                item++;
            }

            if (fila > filaHeader + 1)
            {
                var rangoDatos = ws.Range(filaHeader + 1, 1, fila - 1, 25);
                rangoDatos.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rangoDatos.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rangoDatos.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            ws.Column(6).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Column(7).Style.DateFormat.Format = "dd/MM/yyyy";

            ws.Column(20).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(21).Style.NumberFormat.Format = "#,##0.000";
            ws.Column(22).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(23).Style.NumberFormat.Format = "#,##0.00";
            ws.Column(24).Style.NumberFormat.Format = "#,##0.00";

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(filaHeader);

            var nombreArchivo = $"PackingList_Consolidado_{shipmentPadre}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var contenido = stream.ToArray();

            return File(
                contenido,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                nombreArchivo
            );
        }

        // ======================================================
        // ARMAR PÁGINA PRINCIPAL
        // ======================================================
        private async Task<PackingListDetallePageViewModel> ConstruirPagina(int subtramiteId)
        {
            var subtramite = await _context.Embarques
                .Include(e => e.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .FirstOrDefaultAsync(e => e.Id == subtramiteId && e.EsSubtramite);

            if (subtramite == null)
                return null;

            var respuestasSub = subtramite.Respuestas?.ToList() ?? new List<Respuesta>();

            var requestNo = GetCampoTexto(respuestasSub, "SHIPMENT");
            var fechaEmbarque = GetCampoFecha(respuestasSub, "ETA");
            var fechaArribo = GetCampoFecha(respuestasSub, "FECHA SALIDA AUTORIZADA");

            var packing = await _context.PackingLists
                .Include(p => p.Detalles.Where(d => d.Activo))
                    .ThenInclude(d => d.Modelo)
                .Include(p => p.Detalles.Where(d => d.Activo))
                    .ThenInclude(d => d.Color)
                .FirstOrDefaultAsync(p => p.SubtramiteId == subtramiteId && p.Activo);

            if (packing == null)
            {
                packing = new PackingList
                {
                    SubtramiteId = subtramite.Id,
                    RequestNo = requestNo,
                    FechaEmbarque = fechaEmbarque,
                    FechaArribo = fechaArribo,
                    Activo = true,
                    FechaCreacion = DateTime.Now
                };

                _context.PackingLists.Add(packing);
                await _context.SaveChangesAsync();
            }
            else
            {
                packing.RequestNo = requestNo;
                packing.FechaEmbarque = fechaEmbarque;
                packing.FechaArribo = fechaArribo;
                await _context.SaveChangesAsync();
            }

            packing = await _context.PackingLists
                .Include(p => p.Detalles.Where(d => d.Activo))
                    .ThenInclude(d => d.Modelo)
                .Include(p => p.Detalles.Where(d => d.Activo))
                    .ThenInclude(d => d.Color)
                .FirstOrDefaultAsync(p => p.SubtramiteId == subtramiteId && p.Activo);

            return new PackingListDetallePageViewModel
            {
                Packing = packing,
                NuevoDetalle = new PackingListDetalle
                {
                    PackingListId = packing.Id
                },
                Modelos = await ObtenerModelosPorEmpresaDelSubtramite(subtramite.Id),
                Colores = await _context.Colores
                    .AsNoTracking()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Codigo)
                    .ToListAsync(),
                SubtramiteId = subtramite.Id,
                SubtramiteTitulo = requestNo
            };
        }

        // ======================================================
        // MODELOS PERMITIDOS POR EMPRESA DEL SUBTRÁMITE
        // ======================================================
        private async Task<List<Modelo>> ObtenerModelosPorEmpresaDelSubtramite(int subtramiteId)
        {
            var subtramite = await _context.Embarques
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == subtramiteId);

            if (subtramite == null)
                return new List<Modelo>();

            var empresaId = subtramite.EmpresaId;

            var marcaIds = await _context.MarcaEmpresas
                .AsNoTracking()
                .Where(me => me.EmpresaId == empresaId)
                .Select(me => me.MarcaId)
                .ToListAsync();

            if (marcaIds == null || !marcaIds.Any())
                return new List<Modelo>();

            return await _context.Modelos
                .AsNoTracking()
                .Include(m => m.Marca)
                .Where(m =>
                    m.Activo &&
                    marcaIds.Contains(m.MarcaId))
                .OrderBy(m => m.Marca.Nombre)
                .ThenBy(m => m.Nombre)
                .ToListAsync();
        }
        // ======================================================
        // VALIDAR DETALLE
        // ======================================================
        private void ValidarDetalle(PackingListDetalle detalle, int? idExcluir)
        {
            if (string.IsNullOrWhiteSpace(detalle.VinNo))
                ModelState.AddModelError("NuevoDetalle.VinNo", "El VIN No. es obligatorio.");

            if (string.IsNullOrWhiteSpace(detalle.EngineNumber))
                ModelState.AddModelError("NuevoDetalle.EngineNumber", "El Engine Number es obligatorio.");

            if (!detalle.ModeloId.HasValue || detalle.ModeloId.Value <= 0)
                ModelState.AddModelError("NuevoDetalle.ModeloId", "Debe seleccionar un modelo.");

            if (!detalle.ColorId.HasValue || detalle.ColorId.Value <= 0)
                ModelState.AddModelError("NuevoDetalle.ColorId", "Debe seleccionar un color.");

            if (!string.IsNullOrWhiteSpace(detalle.VinNo))
            {
                var vin = detalle.VinNo.Trim().ToUpper();

                var existeVin = _context.PackingListDetalles
                    .AsNoTracking()
                    .FirstOrDefault(d =>
                        d.Activo &&
                        d.Id != (idExcluir ?? 0) &&
                        d.VinNo != null &&
                        d.VinNo.Trim().ToUpper() == vin);

                if (existeVin != null)
                {
                    ModelState.AddModelError("NuevoDetalle.VinNo",
                        $"El VIN '{detalle.VinNo}' ya existe en la línea {existeVin.Linea} del packing #{existeVin.PackingListId}.");
                }
            }

            if (!string.IsNullOrWhiteSpace(detalle.EngineNumber))
            {
                var engine = detalle.EngineNumber.Trim().ToUpper();

                var existeEngine = _context.PackingListDetalles
                    .AsNoTracking()
                    .FirstOrDefault(d =>
                        d.Activo &&
                        d.Id != (idExcluir ?? 0) &&
                        d.EngineNumber != null &&
                        d.EngineNumber.Trim().ToUpper() == engine);

                if (existeEngine != null)
                {
                    ModelState.AddModelError("NuevoDetalle.EngineNumber",
                        $"El Engine Number '{detalle.EngineNumber}' ya existe en la línea {existeEngine.Linea} del packing #{existeEngine.PackingListId}.");
                }
            }
        }

        // ======================================================
        // AUTOCOMPLETAR DESDE CATÁLOGOS
        // ======================================================
        private async Task CompletarDatosDesdeCatalogos(PackingListDetalle detalle)
        {
            if (detalle.ModeloId.HasValue && detalle.ModeloId.Value > 0)
            {
                var modelo = await _context.Modelos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == detalle.ModeloId.Value);

                if (modelo != null)
                {
                    detalle.ModelPacking = modelo.ModelPacking;
                    detalle.ModelSRI = modelo.ModelSRI;
                    detalle.ModelHomologacion = modelo.ModelHomologacion;
                }
            }

            if (detalle.ColorId.HasValue && detalle.ColorId.Value > 0)
            {
                var color = await _context.Colores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == detalle.ColorId.Value);

                if (color != null)
                {
                    detalle.ColorCodigo = color.Codigo;
                    detalle.ColorName = color.Nombre;
                    detalle.ColorLocal = color.NombreLocal;
                }
            }
        }

        // ======================================================
        // MAPEAR ERRORES DE ÍNDICES
        // ======================================================
        private void MapearErroresIndices(DbUpdateException ex, PackingListDetalle detalle)
        {
            var mensaje = ex.InnerException?.Message ?? ex.Message;

            if (mensaje.Contains("IX_PackingListDetalles_VinNo"))
                ModelState.AddModelError("NuevoDetalle.VinNo", $"El VIN '{detalle.VinNo}' ya existe en el sistema.");
            else if (mensaje.Contains("IX_PackingListDetalles_EngineNumber"))
                ModelState.AddModelError("NuevoDetalle.EngineNumber", $"El Engine Number '{detalle.EngineNumber}' ya existe en el sistema.");
            else
                ModelState.AddModelError("", "Ocurrió un error al guardar la línea del packing: " + mensaje);
        }

        // ======================================================
        // DEBUG DE ERRORES
        // ======================================================
        private List<string> ObtenerErroresModelState()
        {
            return ModelState
                .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                .ToList();
        }

        // ======================================================
        // HELPERS EXCEL
        // ======================================================
        private string NormalizarHeader(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "";

            texto = texto
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Replace("\u00A0", " ")
                .Replace("´", "'")
                .Replace("`", "'")
                .Trim();

            while (texto.Contains("  "))
            {
                texto = texto.Replace("  ", " ");
            }

            return texto.ToUpperInvariant();
        }

        private int BuscarFilaHeader(IXLWorksheet ws)
        {
            for (int fila = 1; fila <= 10; fila++)
            {
                var c1 = NormalizarHeader(ws.Cell(fila, 1).GetString());
                var c2 = NormalizarHeader(ws.Cell(fila, 2).GetString());

                if (c1 == "ITEM" && c2 == "REQUEST NO.")
                {
                    return fila;
                }
            }

            return 0;
        }

        private DateTime? LeerFechaExcel(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty())
                return null;

            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime();

            if (cell.DataType == XLDataType.Number)
            {
                try
                {
                    return DateTime.FromOADate(cell.GetDouble());
                }
                catch
                {
                }
            }

            var texto = cell.GetString().Trim();

            if (string.IsNullOrWhiteSpace(texto))
                return null;

            if (DateTime.TryParse(texto, out var fecha))
                return fecha;

            if (DateTime.TryParseExact(texto, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                return fecha;

            if (DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                return fecha;

            return null;
        }

        private decimal? LeerDecimalExcel(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty())
                return null;

            if (cell.DataType == XLDataType.Number)
                return Convert.ToDecimal(cell.GetDouble());

            var texto = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            if (decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor))
                return valor;

            if (decimal.TryParse(texto, NumberStyles.Any, new CultureInfo("es-EC"), out valor))
                return valor;

            return null;
        }

        // ======================================================
        // HELPERS
        // ======================================================
        private string GetCampoTexto(List<Respuesta> respuestas, string nombre)
        {
            return respuestas.FirstOrDefault(r =>
                r.Pregunta != null &&
                r.Pregunta.Nombre != null &&
                r.Pregunta.Nombre.ToUpper().Contains(nombre.ToUpper())
            )?.Valor ?? "";
        }

        private DateTime? GetCampoFecha(List<Respuesta> respuestas, string nombre)
        {
            var texto = GetCampoTexto(respuestas, nombre);

            if (string.IsNullOrWhiteSpace(texto))
                return null;

            if (DateTime.TryParse(texto, out DateTime fecha))
                return fecha;

            if (DateTime.TryParseExact(texto, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                return fecha;

            if (DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                return fecha;

            return null;
        }
    }
}