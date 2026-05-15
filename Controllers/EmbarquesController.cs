using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Seguimiento.Data;
using Seguimiento.Models;
using Seguimiento.Helpers;

namespace Seguimiento.Controllers
{
    public class EmbarquesController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const string CAMPO_BRAND = "BRAND";
        private const string CAMPO_DEPARTURE = "DEPARTURE";
        private const string CAMPO_ETA = "ETA";
        private const string CAMPO_TRANSITO = "TRANSITO";
        private const string CAMPO_QTY = "QTY";

        private const string CAMPO_FECHA_INGRESO_PESOS = "FECHA INGRESO DE PESOS";
        private const string CAMPO_FECHA_SALIDA_AUTORIZADA = "FECHA SALIDA AUTORIZADA";
        private const string CAMPO_DIAS_ENTRE_INGRESO_SALIDA = "TIEMPO SALIDA AUTORIZADA";

        public EmbarquesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? RolIdSesion()
        {
            return HttpContext.Session.GetInt32("RolId");
        }

        private bool Puede(string accion)
        {
            return PermisoHelper.TienePermiso(_context, RolIdSesion(), "EMBARQUES", accion);
        }

        private static bool CoincideNombre(string nombrePregunta, string textoBuscado)
        {
            if (string.IsNullOrWhiteSpace(nombrePregunta) || string.IsNullOrWhiteSpace(textoBuscado))
                return false;

            return nombrePregunta.Trim().ToUpper().Contains(textoBuscado.Trim().ToUpper());
        }

        private static DateTime? IntentarFecha(string valor)
        {
            return DateTime.TryParse(valor, out DateTime fecha) ? fecha : (DateTime?)null;
        }

        private static string NormalizarStatus(string valor)
        {
            return (valor ?? "").Trim().ToLower();
        }

        private static bool EsCerrado(string status)
        {
            status = NormalizarStatus(status);
            return status == "closed" || status == "cerrado" || status == "entregado";
        }

        private static bool ContieneTexto(string origen, string buscado)
        {
            if (string.IsNullOrWhiteSpace(origen) || string.IsNullOrWhiteSpace(buscado))
                return false;

            return origen.Trim().ToLower().Contains(buscado.Trim().ToLower());
        }

        private Dictionary<string, string> ObtenerRespuestasPadrePrecargadas(int embarquePadreId)
        {
            var respuestasPadre = _context.Respuestas
                .Include(r => r.Pregunta)
                .Where(r => r.EmbarqueId == embarquePadreId && r.Pregunta != null)
                .ToList();

            string ObtenerValor(string nombreCampo)
            {
                return respuestasPadre
                    .FirstOrDefault(r =>
                        r.Pregunta != null &&
                        !string.IsNullOrWhiteSpace(r.Pregunta.Nombre) &&
                        r.Pregunta.Nombre.ToUpper().Contains(nombreCampo.ToUpper()))
                    ?.Valor ?? "";
            }

            return new Dictionary<string, string>
            {
                { "STATUS", ObtenerValor("STATUS") },
                { "BRAND", ObtenerValor("BRAND") },
                { "SHIPMENT", ObtenerValor("SHIPMENT") },
                { "DESTINATION PORT", ObtenerValor("DESTINATION PORT") },
                { "INCOTERM", ObtenerValor("INCOTERM") },
                { "SHIPPING COMPANY", ObtenerValor("SHIPPING COMPANY") }
            };
        }

        private string ObtenerShipmentPadre(int embarquePadreId)
        {
            return _context.Respuestas
                .Include(r => r.Pregunta)
                .Where(r => r.EmbarqueId == embarquePadreId
                         && r.Pregunta != null
                         && r.Pregunta.Nombre != null
                         && r.Pregunta.Nombre.ToUpper().Contains("SHIPMENT"))
                .Select(r => r.Valor)
                .FirstOrDefault() ?? $"#{embarquePadreId}";
        }

        private int ObtenerQtyDesdeFormulario(IFormCollection form, List<Pregunta> preguntas)
        {
            int totalModelos = 0;

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("modelo_"))
                {
                    var valor = form[key].ToString();

                    if (int.TryParse(valor, out int cantidad) && cantidad > 0)
                        totalModelos += cantidad;
                }
            }

            if (totalModelos > 0)
                return totalModelos;

            foreach (var p in preguntas)
            {
                var nombre = p.Nombre?.Trim().ToUpper() ?? "";

                if (nombre == CAMPO_QTY)
                {
                    var valorQty = form["pregunta_" + p.Id].ToString();

                    if (int.TryParse(valorQty, out int qty))
                        return qty;

                    break;
                }
            }

            return 0;
        }

        private int ObtenerQtyEmbarque(int embarqueId)
        {
            var respuestaQty = _context.Respuestas
                .Include(r => r.Pregunta)
                .Where(r => r.EmbarqueId == embarqueId
                         && r.Pregunta != null
                         && r.Pregunta.Nombre != null
                         && r.Pregunta.Nombre.ToUpper().Trim() == CAMPO_QTY)
                .Select(r => r.Valor)
                .FirstOrDefault();

            if (int.TryParse(respuestaQty, out int qtyRespuesta))
                return qtyRespuesta;

            return _context.EmbarqueModelos
                .Where(x => x.EmbarqueId == embarqueId)
                .Sum(x => (int?)x.Cantidad) ?? 0;
        }

        private int ObtenerSumaQtySubtramites(int embarquePadreId, int? excluirEmbarqueId = null)
        {
            var query = _context.Embarques
                .Where(e => e.EsSubtramite && e.EmbarquePadreId == embarquePadreId);

            if (excluirEmbarqueId.HasValue)
                query = query.Where(e => e.Id != excluirEmbarqueId.Value);

            var ids = query.Select(e => e.Id).ToList();

            if (!ids.Any())
                return 0;

            var respuestasQty = _context.Respuestas
                .Include(r => r.Pregunta)
                .Where(r => ids.Contains(r.EmbarqueId)
                         && r.Pregunta != null
                         && r.Pregunta.Nombre != null
                         && r.Pregunta.Nombre.ToUpper().Trim() == CAMPO_QTY)
                .ToList();

            int suma = 0;

            foreach (var id in ids)
            {
                var valorQty = respuestasQty
                    .FirstOrDefault(r => r.EmbarqueId == id)
                    ?.Valor;

                if (int.TryParse(valorQty, out int qty))
                {
                    suma += qty;
                }
                else
                {
                    suma += _context.EmbarqueModelos
                        .Where(x => x.EmbarqueId == id)
                        .Sum(x => (int?)x.Cantidad) ?? 0;
                }
            }

            return suma;
        }

        private void AsignarMensajeValidacionQtySubtramites(int embarquePadreId)
        {
            var qtyPrincipal = ObtenerQtyEmbarque(embarquePadreId);
            var sumaSubtramites = ObtenerSumaQtySubtramites(embarquePadreId);
            var shipmentPadre = ObtenerShipmentPadre(embarquePadreId);

            if (sumaSubtramites == qtyPrincipal)
            {
                TempData["OK"] = $"Correcto: la suma de QTY de los subtrámites del embarque {shipmentPadre} es exactamente {qtyPrincipal}.";
            }
            else
            {
                TempData["OK"] = $"Advertencia: la suma actual de QTY de los subtrámites del embarque {shipmentPadre} es {sumaSubtramites} y el QTY del principal es {qtyPrincipal}.";
            }
        }

        // =========================
        // INDEX
        // =========================
        public async Task<IActionResult> Index()
        {
            if (!Puede("VER"))
                return RedirectToAction("Login", "Account");

            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var lista = await _context.Embarques
                .AsNoTracking()
                .Where(x => x.EmpresaId == empresaId)
                .OrderByDescending(x => x.FechaRegistro)
                .ToListAsync();

            return View(lista);
        }

        // =========================
        // DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard(string filtro, int? marcaId, string q)
        {
            if (!Puede("VER"))
                return RedirectToAction("Login", "Account");

            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var empresa = _context.Empresas
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == empresaId);

            ViewBag.NombreEmpresa = empresa?.Nombre;
            ViewBag.MarcaId = marcaId;
            ViewBag.Filtro = filtro;
            ViewBag.Q = q;

            var embarques = await _context.Embarques
                .Include(x => x.Empresa)
                .Include(x => x.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .Include(x => x.EmbarqueModelos)
                .Where(x => x.EmpresaId == empresaId)
                .ToListAsync();

            var camposObligatorios = new List<string>
            {
                "ETA","TRÁNSITO","HOMOLOGATION","CERTIFICADO 017 INEN","AFIP",
                "INVOICES","BLS","BL","COS","APLICACIONES DE SEGURO","LICENCIA INEN",
                "PACKING","SUBCATEGOR","NOTIFICACIÓN SRI","DESTINO ADUANERO",
                "TIPO DE PAGO","DIAS LIBRES","MRN","PRE-LIQUIDACIÓN","EMISION BL",
                "CAS","PAGO FLETE","ORIGINAL","ESTADO DEL BUQUE"
            };

            ViewBag.MarcasFiltro = _context.Marcas
                .Where(x => x.Activo && x.MarcaEmpresas.Any(me => me.EmpresaId == empresaId))
                .OrderBy(x => x.Nombre)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Nombre
                })
                .ToList();

            string GetValor(Embarque e, string nombreCampo)
            {
                var r = e.Respuestas?.ToList() ?? new List<Respuesta>();

                return r.FirstOrDefault(x =>
                    x.Pregunta != null &&
                    x.Pregunta.Nombre != null &&
                    x.Pregunta.Nombre.ToUpper().Contains(nombreCampo.ToUpper())
                )?.Valor ?? "";
            }

            DateTime? GetFecha(Embarque e, string nombreCampo)
            {
                var valor = GetValor(e, nombreCampo);
                return DateTime.TryParse(valor, out DateTime fecha) ? fecha : (DateTime?)null;
            }

            int? GetMarcaId(Embarque e)
            {
                var valor = GetValor(e, "BRAND");
                return int.TryParse(valor, out int idMarca) ? idMarca : (int?)null;
            }

            bool EsRetrasado(Embarque e)
            {
                var status = NormalizarStatus(GetValor(e, "STATUS"));

                if (EsCerrado(status))
                    return false;

                var departure = GetFecha(e, "DEPARTURE");

                if (!departure.HasValue)
                    return false;

                if (!(departure.Value.Date.AddDays(1) < DateTime.Now.Date))
                    return false;

                foreach (var campo in camposObligatorios)
                {
                    var val = GetValor(e, campo);

                    if (string.IsNullOrWhiteSpace(val))
                        return true;
                }

                return false;
            }

            if (marcaId.HasValue)
            {
                embarques = embarques
                    .Where(e => GetMarcaId(e) == marcaId.Value)
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

                var packings = await _context.PackingLists
                    .Include(p => p.Detalles)
                    .Where(p => p.Activo && subtramitesEmpresaIds.Contains(p.SubtramiteId))
                    .ToListAsync();

                bool CoincideEmbarque(Embarque e, string palabra)
                {
                    if (e.Empresa != null && ContieneTexto(e.Empresa.Nombre, palabra))
                        return true;

                    if (ContieneTexto(e.CreadoPor, palabra) ||
                        ContieneTexto(e.ModificadoPor, palabra))
                        return true;

                    var respuestas = e.Respuestas?.ToList() ?? new List<Respuesta>();

                    foreach (var r in respuestas)
                    {
                        if (r.Pregunta != null && ContieneTexto(r.Pregunta.Nombre, palabra))
                            return true;

                        if (ContieneTexto(r.Valor, palabra))
                            return true;
                    }

                    return false;
                }

                bool CoincidePacking(PackingList p, string palabra)
                {
                    if (ContieneTexto(p.RequestNo, palabra))
                        return true;

                    if (p.FechaEmbarque.HasValue && p.FechaEmbarque.Value.ToString("dd/MM/yyyy").Contains(palabra))
                        return true;

                    if (p.FechaArribo.HasValue && p.FechaArribo.Value.ToString("dd/MM/yyyy").Contains(palabra))
                        return true;

                    var detalles = p.Detalles ?? new List<PackingListDetalle>();

                    foreach (var d in detalles)
                    {
                        if (
                            ContieneTexto(d.OriginPort, palabra) ||
                            ContieneTexto(d.DestinationPort, palabra) ||
                            ContieneTexto(d.Ship, palabra) ||
                            ContieneTexto(d.InvoiceNumber, palabra) ||
                            ContieneTexto(d.BL, palabra) ||
                            ContieneTexto(d.VinNo, palabra) ||
                            ContieneTexto(d.ModelPacking, palabra) ||
                            ContieneTexto(d.ModelSRI, palabra) ||
                            ContieneTexto(d.ModelHomologacion, palabra) ||
                            ContieneTexto(d.EngineCode, palabra) ||
                            ContieneTexto(d.EngineNumber, palabra) ||
                            ContieneTexto(d.ColorCodigo, palabra) ||
                            ContieneTexto(d.ColorName, palabra) ||
                            ContieneTexto(d.ColorLocal, palabra) ||
                            ContieneTexto(d.ModelYear, palabra) ||
                            ContieneTexto(d.Incoterm, palabra) ||
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

            var embarquesOriginal = embarques.ToList();

            var embarquesKpi = embarquesOriginal
                .Where(e => !e.EsSubtramite)
                .ToList();

            var total = embarquesKpi.Count();

            var enTransito = embarquesKpi.Count(e =>
            {
                var status = NormalizarStatus(GetValor(e, "STATUS"));
                return !EsCerrado(status);
            });

            var entregados = embarquesKpi.Count(e =>
            {
                var status = NormalizarStatus(GetValor(e, "STATUS"));
                return EsCerrado(status);
            });

            var retrasados = embarquesKpi.Count(e => EsRetrasado(e));

            var cerrados = embarquesKpi.Count(e =>
            {
                var status = NormalizarStatus(GetValor(e, "STATUS"));
                return EsCerrado(status);
            });

            var abiertos = embarquesKpi.Count(e =>
            {
                var status = NormalizarStatus(GetValor(e, "STATUS"));
                return !EsCerrado(status);
            });

            if (!string.IsNullOrEmpty(filtro) && filtro != "todos")
            {
                embarques = embarques.Where(e =>
                {
                    var status = NormalizarStatus(GetValor(e, "STATUS"));
                    var esRetrasado = EsRetrasado(e);

                    if (filtro == "transito")
                        return !EsCerrado(status);
                    else if (filtro == "entregado")
                        return EsCerrado(status);
                    else if (filtro == "retrasado")
                        return esRetrasado;

                    return true;
                }).ToList();
            }

            var principales = embarques
                .Where(e => !e.EsSubtramite)
                .OrderBy(e => GetFecha(e, "ETA") ?? DateTime.MaxValue)
                .ThenByDescending(e => e.FechaRegistro)
                .ToList();

            var subtramites = embarques
                .Where(e => e.EsSubtramite)
                .OrderBy(e => e.EmbarquePadreId)
                .ThenBy(e => e.OrdenSubtramite ?? int.MaxValue)
                .ThenBy(e => GetFecha(e, "ETA") ?? DateTime.MaxValue)
                .ThenByDescending(e => e.FechaRegistro)
                .ToList();

            var listaOrdenada = new List<Embarque>();

            foreach (var principal in principales)
            {
                listaOrdenada.Add(principal);

                var hijos = subtramites
                    .Where(s => s.EmbarquePadreId == principal.Id)
                    .ToList();

                listaOrdenada.AddRange(hijos);
            }

            ViewBag.Marcas = _context.Marcas.ToList();
            ViewBag.Total = total;
            ViewBag.EnTransito = enTransito;
            ViewBag.Entregados = entregados;
            ViewBag.Retrasados = retrasados;
            ViewBag.Cerrados = cerrados;
            ViewBag.Abiertos = abiertos;

            return View("~/Views/Embarques/Dashboard.cshtml", listaOrdenada);
        }

        // =========================
        // CREATE GET
        // =========================
        public IActionResult Create(int? embarquePadreId)
        {
            if (!Puede("CREAR"))
                return RedirectToAction("Dashboard");

            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var embarque = new Embarque
            {
                EmpresaId = empresaId.Value,
                EsSubtramite = false,
                EmbarquePadreId = null,
                OrdenSubtramite = null
            };

            if (embarquePadreId.HasValue)
            {
                var padre = _context.Embarques
                    .AsNoTracking()
                    .FirstOrDefault(e => e.Id == embarquePadreId.Value && e.EmpresaId == empresaId.Value);

                if (padre == null)
                {
                    TempData["Error"] = "El trámite principal no existe o no pertenece a esta empresa.";
                    return RedirectToAction("Dashboard");
                }

                var siguienteOrden = (_context.Embarques
                    .Where(e => e.EmbarquePadreId == embarquePadreId.Value)
                    .Max(e => (int?)e.OrdenSubtramite) ?? 0) + 1;

                embarque.EsSubtramite = true;
                embarque.EmbarquePadreId = embarquePadreId.Value;
                embarque.OrdenSubtramite = siguienteOrden;

                ViewBag.EsSubtramite = true;
                ViewBag.EmbarquePadreId = embarquePadreId.Value;
                ViewBag.EmbarquePadreShipment = ObtenerShipmentPadre(embarquePadreId.Value);
                ViewBag.OrdenSubtramite = siguienteOrden;
                ViewBag.TituloFormulario = "Crear Subtrámite";
                ViewBag.RespuestasPrecargadasSubtramite = ObtenerRespuestasPadrePrecargadas(embarquePadreId.Value);
            }
            else
            {
                ViewBag.EsSubtramite = false;
                ViewBag.EmbarquePadreId = null;
                ViewBag.EmbarquePadreShipment = null;
                ViewBag.OrdenSubtramite = null;
                ViewBag.TituloFormulario = "Crear Embarque";
                ViewBag.RespuestasPrecargadasSubtramite = new Dictionary<string, string>();
            }

            CargarCombos(empresaId.Value);
            CargarPreguntas();

            ViewBag.EsSoloLectura = false;
            ViewBag.MensajeCierre = null;

            var modelos = _context.Modelos
                .Include(m => m.TipoModelo)
                .Select(m => new
                {
                    m.Id,
                    m.Nombre,
                    m.MarcaId,
                    TipoId = m.TipoModeloId,
                    Tipo = m.TipoModelo.Nombre
                })
                .ToList();

            ViewBag.ModelosJson = modelos;

            return View(embarque);
        }

        // =========================
        // EDIT GET
        // =========================
        public async Task<IActionResult> Edit(int id)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction("Dashboard");

            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var embarque = await _context.Embarques
                .Include(e => e.EmbarqueModelos)
                .Include(e => e.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (embarque == null)
                return NotFound();

            var statusActual = NormalizarStatus(
                embarque.Respuestas
                    .FirstOrDefault(r => r.Pregunta != null && r.Pregunta.Nombre.ToUpper().Contains("STATUS"))
                    ?.Valor
            );

            CargarCombos(empresaId.Value);
            CargarPreguntas();

            ViewBag.EsSoloLectura = EsCerrado(statusActual);
            ViewBag.MensajeCierre = EsCerrado(statusActual)
                ? "El embarque está cerrado. Los datos se muestran solo para consulta y no pueden editarse."
                : null;

            ViewBag.DepartureConfirmado = embarque.Respuestas
                .Any(r => r.PreguntaId == 8 && r.Valor == "CONFIRMADO");

            ViewBag.EtaConfirmado = embarque.Respuestas
                .Any(r => r.PreguntaId == 9 && r.Valor == "CONFIRMADO");

            ViewBag.EsSubtramite = embarque.EsSubtramite;
            ViewBag.EmbarquePadreId = embarque.EmbarquePadreId;
            ViewBag.OrdenSubtramite = embarque.OrdenSubtramite;
            ViewBag.TituloFormulario = embarque.EsSubtramite ? "Editar Subtrámite" : "Editar Embarque";
            ViewBag.RespuestasPrecargadasSubtramite = new Dictionary<string, string>();

            if (embarque.EsSubtramite && embarque.EmbarquePadreId.HasValue)
            {
                ViewBag.EmbarquePadreShipment = ObtenerShipmentPadre(embarque.EmbarquePadreId.Value);
            }
            else
            {
                ViewBag.EmbarquePadreShipment = null;
            }

            var modelos = _context.Modelos
                .Include(m => m.TipoModelo)
                .Select(m => new
                {
                    m.Id,
                    m.Nombre,
                    m.MarcaId,
                    TipoId = m.TipoModeloId,
                    Tipo = m.TipoModelo.Nombre
                })
                .ToList();

            ViewBag.ModelosJson = modelos;
            ViewBag.ModelosSeleccionados = embarque.EmbarqueModelos.ToList();
            ViewBag.Respuestas = embarque.Respuestas.ToList();

            ViewBag.Auditoria = await _context.AuditoriaEmbarques
                .Where(a => a.EmbarqueId == id)
                .OrderByDescending(a => a.Fecha)
                .ToListAsync();

            return View("Create", embarque);
        }

        // =========================
        // CREATE POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormCollection form)
        {
            if (!Puede("CREAR"))
                return RedirectToAction("Dashboard");

            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            int? embarquePadreId = null;
            int? ordenSubtramite = null;
            bool esSubtramite = false;

            if (int.TryParse(form["EmbarquePadreId"], out int padreIdForm) && padreIdForm > 0)
            {
                var padre = await _context.Embarques
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == padreIdForm && e.EmpresaId == empresaId.Value);

                if (padre == null)
                {
                    TempData["Error"] = "El trámite principal no existe o no pertenece a esta empresa.";
                    return RedirectToAction("Dashboard");
                }

                embarquePadreId = padreIdForm;
                esSubtramite = true;

                ordenSubtramite = (_context.Embarques
                    .Where(e => e.EmbarquePadreId == padreIdForm)
                    .Max(e => (int?)e.OrdenSubtramite) ?? 0) + 1;
            }

            var preguntas = _context.Preguntas
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToList();

            DateTime? fechaDeparture = null;
            DateTime? fechaEta = null;
            DateTime? fechaIngresoPesos = null;
            DateTime? fechaSalidaAutorizada = null;
            int marcaId = 0;
            int qtyActualSubtramite = ObtenerQtyDesdeFormulario(form, preguntas);

            foreach (var p in preguntas)
            {
                var valor = form["pregunta_" + p.Id].ToString();

                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                var nombre = p.Nombre?.Trim().ToUpper() ?? "";

                if (nombre == CAMPO_BRAND)
                    int.TryParse(valor, out marcaId);

                if (DateTime.TryParse(valor, out DateTime fecha))
                {
                    if (CoincideNombre(nombre, CAMPO_DEPARTURE))
                        fechaDeparture = fecha;

                    if (CoincideNombre(nombre, CAMPO_ETA))
                        fechaEta = fecha;

                    if (CoincideNombre(nombre, CAMPO_FECHA_INGRESO_PESOS))
                        fechaIngresoPesos = fecha;

                    if (CoincideNombre(nombre, CAMPO_FECHA_SALIDA_AUTORIZADA))
                        fechaSalidaAutorizada = fecha;
                }
            }

            void PrepararVistaError()
            {
                CargarCombos(empresaId.Value);
                CargarPreguntas();

                ViewBag.EsSoloLectura = false;
                ViewBag.MensajeCierre = null;
                ViewBag.EsSubtramite = esSubtramite;
                ViewBag.EmbarquePadreId = embarquePadreId;
                ViewBag.OrdenSubtramite = ordenSubtramite;
                ViewBag.TituloFormulario = esSubtramite ? "Crear Subtrámite" : "Crear Embarque";
                ViewBag.EmbarquePadreShipment = embarquePadreId.HasValue ? ObtenerShipmentPadre(embarquePadreId.Value) : null;
                ViewBag.RespuestasPrecargadasSubtramite = embarquePadreId.HasValue
                    ? ObtenerRespuestasPadrePrecargadas(embarquePadreId.Value)
                    : new Dictionary<string, string>();

                var modelosError = _context.Modelos
                    .Include(m => m.TipoModelo)
                    .Select(m => new
                    {
                        m.Id,
                        m.Nombre,
                        m.MarcaId,
                        TipoId = m.TipoModeloId,
                        Tipo = m.TipoModelo.Nombre
                    })
                    .ToList();

                ViewBag.ModelosJson = modelosError;
            }

            if (marcaId == 0)
            {
                ModelState.AddModelError("", "Debe seleccionar una marca");
                PrepararVistaError();

                return View(new Embarque
                {
                    EmpresaId = empresaId.Value,
                    EsSubtramite = esSubtramite,
                    EmbarquePadreId = embarquePadreId,
                    OrdenSubtramite = ordenSubtramite
                });
            }

            if (fechaDeparture.HasValue && fechaEta.HasValue && fechaEta < fechaDeparture)
            {
                ModelState.AddModelError("", "ETA no puede ser menor que Departure");
                PrepararVistaError();

                return View(new Embarque
                {
                    EmpresaId = empresaId.Value,
                    EsSubtramite = esSubtramite,
                    EmbarquePadreId = embarquePadreId,
                    OrdenSubtramite = ordenSubtramite
                });
            }

            if (fechaIngresoPesos.HasValue && fechaSalidaAutorizada.HasValue && fechaSalidaAutorizada < fechaIngresoPesos)
            {
                ModelState.AddModelError("", "FECHA SALIDA AUTORIZADA no puede ser menor que FECHA INGRESO DE PESOS");
                PrepararVistaError();

                return View(new Embarque
                {
                    EmpresaId = empresaId.Value,
                    EsSubtramite = esSubtramite,
                    EmbarquePadreId = embarquePadreId,
                    OrdenSubtramite = ordenSubtramite
                });
            }

            if (esSubtramite && embarquePadreId.HasValue)
            {
                int qtyPrincipal = ObtenerQtyEmbarque(embarquePadreId.Value);
                int qtyExistenteSubtramites = ObtenerSumaQtySubtramites(embarquePadreId.Value);
                int nuevoTotalSubtramites = qtyExistenteSubtramites + qtyActualSubtramite;

                if (nuevoTotalSubtramites > qtyPrincipal)
                {
                    ModelState.AddModelError("", $"La suma de QTY de los subtrámites ({nuevoTotalSubtramites}) no puede superar el QTY del embarque principal ({qtyPrincipal}).");
                    PrepararVistaError();

                    return View(new Embarque
                    {
                        EmpresaId = empresaId.Value,
                        EsSubtramite = esSubtramite,
                        EmbarquePadreId = embarquePadreId,
                        OrdenSubtramite = ordenSubtramite
                    });
                }
            }

            var embarque = new Embarque
            {
                EmpresaId = empresaId.Value,
                FechaRegistro = DateTime.Now,
                CreadoPor = HttpContext.Session.GetString("Usuario"),
                FechaCreacion = DateTime.Now,
                EsSubtramite = esSubtramite,
                EmbarquePadreId = embarquePadreId,
                OrdenSubtramite = ordenSubtramite
            };

            _context.Embarques.Add(embarque);
            await _context.SaveChangesAsync();

            _context.AuditoriaEmbarques.Add(new AuditoriaEmbarque
            {
                EmbarqueId = embarque.Id,
                Usuario = HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema",
                Accion = "CREACIÓN",
                Fecha = DateTime.Now,
                Detalle = esSubtramite
                    ? $"Se creó el subtrámite #{ordenSubtramite} del embarque padre {ObtenerShipmentPadre(embarquePadreId ?? 0)}"
                    : "Se creó el embarque"
            });

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("modelo_"))
                {
                    var modeloId = int.Parse(key.Replace("modelo_", ""));
                    var valor = form[key];

                    if (!string.IsNullOrWhiteSpace(valor))
                    {
                        int cantidad = int.Parse(valor);

                        if (cantidad > 0)
                        {
                            _context.EmbarqueModelos.Add(new EmbarqueModelo
                            {
                                EmbarqueId = embarque.Id,
                                ModeloId = modeloId,
                                Cantidad = cantidad
                            });
                        }
                    }
                }
            }

            foreach (var p in preguntas)
            {
                var valor = form["pregunta_" + p.Id].ToString();

                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                var nombre = p.Nombre?.Trim().ToUpper() ?? "";

                if (CoincideNombre(nombre, CAMPO_TRANSITO))
                    continue;

                if (CoincideNombre(nombre, CAMPO_DIAS_ENTRE_INGRESO_SALIDA))
                    continue;

                _context.Respuestas.Add(new Respuesta
                {
                    EmbarqueId = embarque.Id,
                    PreguntaId = p.Id,
                    Valor = valor.Trim(),
                    FechaRegistro = DateTime.Now
                });
            }

            if (fechaDeparture.HasValue && fechaEta.HasValue)
            {
                var dias = (fechaEta.Value - fechaDeparture.Value).Days;

                var preguntaTransito = preguntas
                    .FirstOrDefault(x => CoincideNombre(x.Nombre, CAMPO_TRANSITO));

                if (preguntaTransito != null)
                {
                    _context.Respuestas.Add(new Respuesta
                    {
                        EmbarqueId = embarque.Id,
                        PreguntaId = preguntaTransito.Id,
                        Valor = dias.ToString(),
                        FechaRegistro = DateTime.Now
                    });
                }
            }

            if (fechaIngresoPesos.HasValue && fechaSalidaAutorizada.HasValue)
            {
                var diasEntre = (fechaSalidaAutorizada.Value - fechaIngresoPesos.Value).Days;

                var preguntaDiasEntre = preguntas
                    .FirstOrDefault(x => CoincideNombre(x.Nombre, CAMPO_DIAS_ENTRE_INGRESO_SALIDA));

                if (preguntaDiasEntre != null)
                {
                    _context.Respuestas.Add(new Respuesta
                    {
                        EmbarqueId = embarque.Id,
                        PreguntaId = preguntaDiasEntre.Id,
                        Valor = diasEntre.ToString(),
                        FechaRegistro = DateTime.Now
                    });
                }
            }

            embarque.DepartureConfirmado = form["departureConfirmado"] == "true";
            embarque.EtaConfirmado = form["etaConfirmado"] == "true";

            await _context.SaveChangesAsync();

            if (esSubtramite && embarquePadreId.HasValue)
            {
                AsignarMensajeValidacionQtySubtramites(embarquePadreId.Value);
            }
            else
            {
                TempData["OK"] = "Embarque guardado correctamente.";
            }

            return RedirectToAction("Dashboard");
        }

        // =========================
        // EDIT POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormCollection form)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction("Dashboard");

            var embarque = await _context.Embarques.FindAsync(id);

            if (embarque == null)
                return NotFound();

            var respuestasActuales = _context.Respuestas
                .Where(r => r.EmbarqueId == id)
                .ToList();

            var preguntaStatus = _context.Preguntas
                .FirstOrDefault(p => p.Nombre.ToUpper().Contains("STATUS"));

            if (preguntaStatus != null)
            {
                var statusActual = NormalizarStatus(
                    respuestasActuales.FirstOrDefault(r => r.PreguntaId == preguntaStatus.Id)?.Valor
                );

                var statusNuevo = NormalizarStatus(
                    form["pregunta_" + preguntaStatus.Id].ToString()
                );

                if (EsCerrado(statusActual))
                {
                    TempData["Error"] = "El embarque está cerrado y no puede modificarse.";
                    return RedirectToAction("Dashboard");
                }

                if (EsCerrado(statusNuevo))
                {
                    // Se guarda y desde el siguiente intento quedará bloqueado.
                }
            }

            var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema";
            var preguntas = _context.Preguntas
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToList();

            int qtyActualSubtramite = ObtenerQtyDesdeFormulario(form, preguntas);

            DateTime? fechaDeparture = null;
            DateTime? fechaEta = null;
            DateTime? fechaIngresoPesos = null;
            DateTime? fechaSalidaAutorizada = null;

            foreach (var p in preguntas)
            {
                var valor = form["pregunta_" + p.Id].ToString();

                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                var nombre = p.Nombre?.Trim().ToUpper() ?? "";

                if (DateTime.TryParse(valor, out DateTime fecha))
                {
                    if (CoincideNombre(nombre, CAMPO_DEPARTURE))
                        fechaDeparture = fecha;

                    if (CoincideNombre(nombre, CAMPO_ETA))
                        fechaEta = fecha;

                    if (CoincideNombre(nombre, CAMPO_FECHA_INGRESO_PESOS))
                        fechaIngresoPesos = fecha;

                    if (CoincideNombre(nombre, CAMPO_FECHA_SALIDA_AUTORIZADA))
                        fechaSalidaAutorizada = fecha;
                }
            }

            if (fechaDeparture.HasValue && fechaEta.HasValue && fechaEta < fechaDeparture)
            {
                TempData["Error"] = "ETA no puede ser menor que Departure.";
                return RedirectToAction("Edit", new { id });
            }

            if (fechaIngresoPesos.HasValue && fechaSalidaAutorizada.HasValue && fechaSalidaAutorizada < fechaIngresoPesos)
            {
                TempData["Error"] = "FECHA SALIDA AUTORIZADA no puede ser menor que FECHA INGRESO DE PESOS.";
                return RedirectToAction("Edit", new { id });
            }

            if (embarque.EsSubtramite && embarque.EmbarquePadreId.HasValue)
            {
                int qtyPrincipal = ObtenerQtyEmbarque(embarque.EmbarquePadreId.Value);
                int qtyOtrosSubtramites = ObtenerSumaQtySubtramites(embarque.EmbarquePadreId.Value, id);
                int nuevoTotalSubtramites = qtyOtrosSubtramites + qtyActualSubtramite;

                if (nuevoTotalSubtramites > qtyPrincipal)
                {
                    TempData["Error"] = $"La suma de QTY de los subtrámites ({nuevoTotalSubtramites}) no puede superar el QTY del embarque principal ({qtyPrincipal}).";
                    return RedirectToAction("Edit", new { id });
                }
            }

            foreach (var p in preguntas)
            {
                var nuevoValor = form["pregunta_" + p.Id].ToString()?.Trim();

                var actual = respuestasActuales
                    .FirstOrDefault(r => r.PreguntaId == p.Id);

                var valorAnterior = actual?.Valor?.Trim();

                var nombre = p.Nombre?.Trim().ToUpper() ?? "";

                if (CoincideNombre(nombre, CAMPO_DIAS_ENTRE_INGRESO_SALIDA) ||
                    CoincideNombre(nombre, CAMPO_TRANSITO))
                {
                    continue;
                }

                if (nuevoValor != valorAnterior)
                {
                    string detalle = $"{p.Nombre}: '{valorAnterior ?? "VACÍO"}' → '{nuevoValor ?? "VACÍO"}'";

                    _context.AuditoriaEmbarques.Add(new AuditoriaEmbarque
                    {
                        EmbarqueId = id,
                        Usuario = usuarioNombre,
                        Accion = "MODIFICACIÓN",
                        Fecha = DateTime.Now,
                        Detalle = detalle
                    });
                }
            }

            _context.EmbarqueModelos.RemoveRange(
                _context.EmbarqueModelos.Where(x => x.EmbarqueId == id)
            );

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("modelo_"))
                {
                    var modeloId = int.Parse(key.Replace("modelo_", ""));
                    var valor = form[key];

                    if (!string.IsNullOrWhiteSpace(valor))
                    {
                        int cantidad = int.Parse(valor);

                        if (cantidad > 0)
                        {
                            _context.EmbarqueModelos.Add(new EmbarqueModelo
                            {
                                EmbarqueId = id,
                                ModeloId = modeloId,
                                Cantidad = cantidad
                            });
                        }
                    }
                }
            }

            _context.Respuestas.RemoveRange(
                _context.Respuestas.Where(r => r.EmbarqueId == id)
            );

            foreach (var p in preguntas)
            {
                var valor = form["pregunta_" + p.Id].ToString();

                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                var nombre = p.Nombre?.Trim().ToUpper() ?? "";

                if (CoincideNombre(nombre, CAMPO_TRANSITO))
                    continue;

                if (CoincideNombre(nombre, CAMPO_DIAS_ENTRE_INGRESO_SALIDA))
                    continue;

                _context.Respuestas.Add(new Respuesta
                {
                    EmbarqueId = id,
                    PreguntaId = p.Id,
                    Valor = valor.Trim(),
                    FechaRegistro = DateTime.Now
                });
            }

            if (fechaDeparture.HasValue && fechaEta.HasValue)
            {
                var dias = (fechaEta.Value - fechaDeparture.Value).Days;

                var preguntaTransito = preguntas
                    .FirstOrDefault(x => CoincideNombre(x.Nombre, CAMPO_TRANSITO));

                if (preguntaTransito != null)
                {
                    _context.Respuestas.Add(new Respuesta
                    {
                        EmbarqueId = id,
                        PreguntaId = preguntaTransito.Id,
                        Valor = dias.ToString(),
                        FechaRegistro = DateTime.Now
                    });
                }
            }

            if (fechaIngresoPesos.HasValue && fechaSalidaAutorizada.HasValue)
            {
                var diasEntre = (fechaSalidaAutorizada.Value - fechaIngresoPesos.Value).Days;

                var preguntaDiasEntre = preguntas
                    .FirstOrDefault(x => CoincideNombre(x.Nombre, CAMPO_DIAS_ENTRE_INGRESO_SALIDA));

                if (preguntaDiasEntre != null)
                {
                    _context.Respuestas.Add(new Respuesta
                    {
                        EmbarqueId = id,
                        PreguntaId = preguntaDiasEntre.Id,
                        Valor = diasEntre.ToString(),
                        FechaRegistro = DateTime.Now
                    });
                }
            }

            embarque.ModificadoPor = usuarioNombre;
            embarque.FechaModificacion = DateTime.Now;
            embarque.DepartureConfirmado = form["departureConfirmado"] == "true";
            embarque.EtaConfirmado = form["etaConfirmado"] == "true";

            await _context.SaveChangesAsync();

            if (embarque.EsSubtramite && embarque.EmbarquePadreId.HasValue)
            {
                AsignarMensajeValidacionQtySubtramites(embarque.EmbarquePadreId.Value);
            }
            else
            {
                TempData["OK"] = "Embarque actualizado correctamente.";
            }

            return RedirectToAction("Dashboard");
        }

        // =========================
        // DETAILS
        // =========================
        public async Task<IActionResult> Details(int id)
        {
            if (!Puede("VER"))
                return RedirectToAction("Login", "Account");

            var preguntas = await _context.Preguntas
                .OrderBy(p => p.Orden)
                .ToListAsync();

            ViewBag.Preguntas = preguntas;

            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var embarque = await _context.Embarques
                .Include(e => e.Empresa)
                .Include(e => e.EmbarqueModelos)
                    .ThenInclude(em => em.Modelo)
                        .ThenInclude(m => m.TipoModelo)
                .Include(e => e.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (embarque == null)
                return NotFound();

            ViewBag.Marcas = _context.Marcas.ToList();

            return View(embarque);
        }

        // =========================
        // DELETE GET
        // =========================
        public async Task<IActionResult> Delete(int id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction("Dashboard");

            var embarque = await _context.Embarques
                .Include(e => e.Empresa)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (embarque == null)
                return NotFound();

            return View(embarque);
        }

        // =========================
        // DELETE POST
        // =========================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction("Dashboard");

            var embarque = await _context.Embarques.FindAsync(id);

            if (embarque != null)
            {
                _context.EmbarqueModelos.RemoveRange(
                    _context.EmbarqueModelos.Where(x => x.EmbarqueId == id)
                );

                _context.Respuestas.RemoveRange(
                    _context.Respuestas.Where(x => x.EmbarqueId == id)
                );

                _context.Embarques.Remove(embarque);
                await _context.SaveChangesAsync();

                if (embarque.EsSubtramite && embarque.EmbarquePadreId.HasValue)
                {
                    AsignarMensajeValidacionQtySubtramites(embarque.EmbarquePadreId.Value);
                }
            }

            return RedirectToAction("Dashboard");
        }

        // =========================
        // COMBOS
        // =========================
        private void CargarCombos(int empresaId)
        {
            ViewBag.Marcas = _context.Marcas
                .Where(x => x.Activo && x.MarcaEmpresas.Any(me => me.EmpresaId == empresaId))
                .OrderBy(x => x.Nombre)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Nombre
                })
                .ToList();
        }

        private void CargarPreguntas()
        {
            ViewBag.Preguntas = _context.Preguntas
                .Where(p => p.Activo)
                .OrderBy(p => p.Orden)
                .ToList();

            ViewBag.Opciones = _context.OpcionesLista
                .Where(o => o.Activo)
                .ToList();
        }
    }
}