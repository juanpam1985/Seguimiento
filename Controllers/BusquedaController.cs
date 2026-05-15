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
    public class BusquedaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BusquedaController(ApplicationDbContext context)
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

        private static string NormalizarTexto(string valor)
        {
            return (valor ?? "").Trim().ToLower();
        }

        private static bool ContieneTexto(string origen, string buscado)
        {
            if (string.IsNullOrWhiteSpace(origen) || string.IsNullOrWhiteSpace(buscado))
                return false;

            return origen.Trim().ToLower().Contains(buscado.Trim().ToLower());
        }

        private static bool EsCerrado(string status)
        {
            status = NormalizarTexto(status);
            return status == "closed" || status == "cerrado" || status == "entregado";
        }

        private static DateTime? IntentarFecha(string valor)
        {
            return DateTime.TryParse(valor, out DateTime fecha) ? fecha : (DateTime?)null;
        }

        private static bool CoincideNombrePregunta(string nombrePregunta, string buscado)
        {
            if (string.IsNullOrWhiteSpace(nombrePregunta) || string.IsNullOrWhiteSpace(buscado))
                return false;

            return nombrePregunta.Trim().ToUpper().Contains(buscado.Trim().ToUpper());
        }

        public async Task<IActionResult> Index(string q, string filtro, int? marcaId)
        {
            if (!Puede("VER"))
                return RedirectToAction("Login", "Account");

            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var empresa = await _context.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == empresaId.Value);

            ViewBag.NombreEmpresa = empresa?.Nombre;
            ViewBag.MarcaId = marcaId;
            ViewBag.Filtro = filtro;
            ViewBag.Q = q;

            var embarques = await _context.Embarques
                .Include(x => x.Empresa)
                .Include(x => x.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .Include(x => x.EmbarqueModelos)
                .Where(x => x.EmpresaId == empresaId.Value)
                .ToListAsync();

            ViewBag.MarcasFiltro = await _context.Marcas
                .Where(x => x.Activo && x.MarcaEmpresas.Any(me => me.EmpresaId == empresaId.Value))
                .OrderBy(x => x.Nombre)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Nombre
                })
                .ToListAsync();

            string GetValor(Embarque e, string nombreCampo)
            {
                var respuestas = e.Respuestas?.ToList() ?? new List<Respuesta>();

                return respuestas.FirstOrDefault(x =>
                    x.Pregunta != null &&
                    x.Pregunta.Nombre != null &&
                    x.Pregunta.Nombre.ToUpper().Contains(nombreCampo.ToUpper())
                )?.Valor ?? "";
            }

            DateTime? GetFecha(Embarque e, string nombreCampo)
            {
                var valor = GetValor(e, nombreCampo);
                return IntentarFecha(valor);
            }

            int? GetMarcaId(Embarque e)
            {
                var valor = GetValor(e, "BRAND");
                return int.TryParse(valor, out int idMarca) ? idMarca : (int?)null;
            }

            bool EsRetrasado(Embarque e)
            {
                var camposObligatorios = new List<string>
                {
                    "ETA","TRÁNSITO","HOMOLOGATION","CERTIFICADO 017 INEN","AFIP",
                    "INVOICES","BLS","BL","COS","APLICACIONES DE SEGURO","LICENCIA INEN",
                    "PACKING","SUBCATEGOR","NOTIFICACIÓN SRI","DESTINO ADUANERO",
                    "TIPO DE PAGO","DIAS LIBRES","MRN","PRE-LIQUIDACIÓN","EMISION BL",
                    "CAS","PAGO FLETE","ORIGINAL","ESTADO DEL BUQUE"
                };

                var status = NormalizarTexto(GetValor(e, "STATUS"));

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

            // ==========================================
            // FILTRO POR MARCA
            // ==========================================
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
                        if (r.Pregunta != null)
                        {
                            if (ContieneTexto(r.Pregunta.Nombre, palabra))
                                return true;
                        }

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

                // Si la coincidencia fue en packing list, asegurar incluir el subtrámite y su padre
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

            // ==========================================
            // KPI ANTES DEL FILTRO VISUAL
            // ==========================================
            var embarquesOriginal = embarques.ToList();

            var embarquesKpi = embarquesOriginal
                .Where(e => !e.EsSubtramite)
                .ToList();

            var total = embarquesKpi.Count();

            var enTransito = embarquesKpi.Count(e =>
            {
                var status = NormalizarTexto(GetValor(e, "STATUS"));
                return !EsCerrado(status);
            });

            var entregados = embarquesKpi.Count(e =>
            {
                var status = NormalizarTexto(GetValor(e, "STATUS"));
                return EsCerrado(status);
            });

            var retrasados = embarquesKpi.Count(e => EsRetrasado(e));

            var cerrados = embarquesKpi.Count(e =>
            {
                var status = NormalizarTexto(GetValor(e, "STATUS"));
                return EsCerrado(status);
            });

            var abiertos = embarquesKpi.Count(e =>
            {
                var status = NormalizarTexto(GetValor(e, "STATUS"));
                return !EsCerrado(status);
            });

            // ==========================================
            // FILTRO VISUAL DEL DASHBOARD
            // ==========================================
            if (!string.IsNullOrEmpty(filtro) && filtro != "todos")
            {
                embarques = embarques.Where(e =>
                {
                    var status = NormalizarTexto(GetValor(e, "STATUS"));
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

            ViewBag.Marcas = await _context.Marcas.ToListAsync();
            ViewBag.Total = total;
            ViewBag.EnTransito = enTransito;
            ViewBag.Entregados = entregados;
            ViewBag.Retrasados = retrasados;
            ViewBag.Cerrados = cerrados;
            ViewBag.Abiertos = abiertos;

            // Reutiliza tu misma vista del dashboard
            return View("~/Views/Embarques/Dashboard.cshtml", listaOrdenada);
        }
    }
}