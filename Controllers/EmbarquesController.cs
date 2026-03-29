using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Seguimiento.Data;
using Seguimiento.Models;

namespace Seguimiento.Controllers
{
    public class EmbarquesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmbarquesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // INDEX
        // =========================
        public async Task<IActionResult> Index()
        {
            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var lista = await _context.Embarques
                .Where(x => x.EmpresaId == empresaId)
                .OrderByDescending(x => x.FechaRegistro)
                .ToListAsync();

            return View(lista);
        }

        // =========================
        // DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard(string filtro)
        {
            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var empresa = _context.Empresas
                .FirstOrDefault(x => x.Id == empresaId);

            ViewBag.NombreEmpresa = empresa?.Nombre;

            var embarques = await _context.Embarques


             .Include(x => x.Empresa)
             .Include(x => x.Respuestas)
             .ThenInclude(r => r.Pregunta)
            .Include(x => x.EmbarqueModelos)
            .Where(x => x.EmpresaId == empresaId)
            .OrderByDescending(x => x.FechaRegistro)
            .ToListAsync();
            var embarquesOriginal = embarques.ToList();
            //real
            var camposObligatorios = new List<string>
{
    "ETA","TRÁNSITO","HOMOLOGATION","CERTIFICADO 017 INEN","AFIP",
    "INVOICES","BLs","BL","COs","APLICACIONES DE SEGURO","LICENCIA INEN",
    "PACKING","SUBCATEGOR","NOTIFICACIÓN SRI","DESTINO ADUANERO",
    "TIPO DE PAGO","DIAS LIBRES","MRN","PRE-LIQUIDACIÓN","EMISION BL",
    "CAS","PAGO FLETE","ORIGINAL","ESTADO DEL BUQUE"
};
            ////DESDE ACA FILTRA
            ///// =========================
            // 🔥 FILTRO DESDE KPI
            // =========================
            if (!string.IsNullOrEmpty(filtro) && filtro != "todos")
            {
                // 🔥 CAMPOS OBLIGATORIOS
             
                embarques = embarques.Where(e =>
                {
                    var r = e.Respuestas?.ToList() ?? new List<Respuesta>();

                    var status = r.FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains("STATUS"))?.Valor?.ToLower();
                    var departure = r.FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))?.Valor;

                    bool esRetrasado = false;

                    if (DateTime.TryParse(departure, out DateTime depDate) && depDate < DateTime.Now && status != "closed")
                    {
                    

                        foreach (var campo in camposObligatorios)
                        {
                            var val = r.FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains(campo))?.Valor;
                            if (string.IsNullOrWhiteSpace(val))
                            {
                                esRetrasado = true;
                                break;
                            }
                        }
                    }
                    if (filtro == "transito")
                    {
                        var dep = r.FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))?.Valor;

                        bool esRetrasadoLocal = false;

                        // 🔥 SOLO evaluamos si tiene fecha
                        if (DateTime.TryParse(dep, out DateTime fechaDep))
                        {
                            // 🔥 SI YA PASÓ EL TIEMPO
                            if (fechaDep.Date.AddDays(1) < DateTime.Now.Date)
                            {
                                foreach (var campo in camposObligatorios)
                                {
                                    var val = r.FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains(campo))?.Valor;

                                    if (string.IsNullOrWhiteSpace(val))
                                    {
                                        esRetrasadoLocal = true;
                                        break;
                                    }
                                }
                            }
                        }

                        // 🔥 CLAVE: TODO LO QUE NO ES RETRASADO → ES TRANSITO
                        return !esRetrasadoLocal;
                    }
                    else if (filtro == "entregado")
                    {
                        return status == "entregado" || status == "closed";
                    }
                    else if (filtro == "retrasado")
                    {
                        return esRetrasado;
                    }

                    return true;
                }).ToList();
            }
            ///HASTA ACA
            // =========================
            // 🔥 KPI DASHBOARD
            // =========================

            // TOTAL
            var total = embarquesOriginal.Count();

            // EN TRÁNSITO
            var enTransito = embarquesOriginal.Count(e =>
            {
                var r = e.Respuestas?.ToList() ?? new List<Respuesta>();

                var departure = r.FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))?.Valor;

                bool esRetrasado = false;

                if (DateTime.TryParse(departure, out DateTime depDate))
                {
                    if (depDate.Date.AddDays(1) < DateTime.Now.Date)
                    {
                        foreach (var campo in camposObligatorios)
                        {
                            var val = r.FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains(campo))?.Valor;

                            if (string.IsNullOrWhiteSpace(val))
                            {
                                esRetrasado = true;
                                break;
                            }
                        }
                    }
                }

                // 🔥 CLAVE FINAL
                return !esRetrasado;
            });

            // ENTREGADOS
            var entregados = embarquesOriginal.Count(e =>
            {
                var status = e.Respuestas
                    .FirstOrDefault(r => r.Pregunta.Nombre.ToUpper().Contains("STATUS"))
                    ?.Valor?.ToLower();

                return status == "entregado" || status == "closed";
            });

  

            // 🔥 KPI
            var retrasados = embarquesOriginal.Count(e =>
            {
                var r = e.Respuestas?.ToList() ?? new List<Respuesta>();

                var departure = r
                    .FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))
                    ?.Valor;

                if (!DateTime.TryParse(departure, out DateTime depDate))
                    return false;

                if (!(depDate.Date.AddDays(1) < DateTime.Now.Date))
                    return false;

                var camposObligatorios = new List<string>
    {
        "ETA","TRÁNSITO","HOMOLOGATION","CERTIFICADO 017 INEN","AFIP",
        "INVOICES","BLs","BL","COs","APLICACIONES DE SEGURO","LICENCIA INEN",
        "PACKING","SUBCATEGOR","NOTIFICACIÓN SRI","DESTINO ADUANERO",
        "TIPO DE PAGO","DIAS LIBRES","MRN","PRE-LIQUIDACIÓN","EMISION BL",
        "CAS","PAGO FLETE","ORIGINAL","ESTADO DEL BUQUE"
    };

                foreach (var campo in camposObligatorios)
                {
                    var val = r
                        .FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains(campo))
                        ?.Valor;

                    if (string.IsNullOrWhiteSpace(val))
                        return true; // 🔥 ES RETRASADO
                }

                return false;
            });
            ViewBag.Marcas = _context.Marcas.ToList();
            ViewBag.Total = total;
            ViewBag.EnTransito = enTransito;
            ViewBag.Entregados = entregados;
            ViewBag.Retrasados = retrasados;
            return View(embarques);
        }
        private int ObtenerRol()
        {
            return HttpContext.Session.GetInt32("RolId") ?? 0;
        }
        // =========================
        // CREATE GET
        // =========================
        public IActionResult Create()
        {
            var rol = ObtenerRol();

            if (rol == 3) // CLIENTE NO PUEDE
                return RedirectToAction("Dashboard");
            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            CargarCombos(empresaId.Value);
            CargarPreguntas();

            // 🔥 FIX: incluir TipoModelo y enviar TipoId
            var modelos = _context.Modelos
                .Include(m => m.TipoModelo)
                .Select(m => new
                {
                    m.Id,
                    m.Nombre,
                    m.MarcaId,
                    TipoId = m.TipoModeloId, // 🔥 CLAVE
                    Tipo = m.TipoModelo.Nombre
                })
                .ToList();

            ViewBag.ModelosJson = modelos;

            return View(new Embarque());
        }

        // =========================
        // EDIT GET
        // =========================
        public async Task<IActionResult> Edit(int id)
        {
            var rol = ObtenerRol();

            if (rol == 3)
                return RedirectToAction("Dashboard");
            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");
            if (rol != 1)
                return RedirectToAction("Dashboard");
            var embarque = await _context.Embarques
                .Include(e => e.EmbarqueModelos)
                .Include(e => e.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (embarque == null)
                return NotFound();

            CargarCombos(empresaId.Value);
            CargarPreguntas();
            ViewBag.DepartureConfirmado = embarque.Respuestas
    .Any(r => r.PreguntaId == 8 && r.Valor == "CONFIRMADO");

            ViewBag.EtaConfirmado = embarque.Respuestas
                .Any(r => r.PreguntaId == 9 && r.Valor == "CONFIRMADO");
            // =========================
            // 🔥 MODELOS (FIX DINÁMICO)
            // =========================
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

            // =========================
            // 🔥 DATOS ACTUALES
            // =========================
            ViewBag.ModelosSeleccionados = embarque.EmbarqueModelos.ToList();
            ViewBag.Respuestas = embarque.Respuestas.ToList();

            // =========================
            // 🔥 AUDITORÍA (HISTORIAL)
            // =========================
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
            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var embarque = new Embarque
            {
                EmpresaId = empresaId.Value,
                FechaRegistro = DateTime.Now,
                CreadoPor = HttpContext.Session.GetString("Usuario"),
                FechaCreacion = DateTime.Now
            };

            _context.Embarques.Add(embarque);
            await _context.SaveChangesAsync();

            // =========================
            // 🔥 AUDITORÍA CREACIÓN
            // =========================
            _context.AuditoriaEmbarques.Add(new AuditoriaEmbarque
            {
                EmbarqueId = embarque.Id,
                Usuario = HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema",
                Accion = "CREACIÓN",
                Fecha = DateTime.Now,
                Detalle = "Se creó el embarque"
            });

            // =========================
            // MODELOS
            // =========================
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

            var preguntas = _context.Preguntas.Where(x => x.Activo).ToList();

            DateTime? fechaDeparture = null;
            DateTime? fechaEta = null;
            int marcaId = 0;

            // =========================
            // DETECTAR FECHAS Y BRAND
            // =========================
            foreach (var p in preguntas)
            {
                var valor = form["pregunta_" + p.Id].ToString();

                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                var nombre = p.Nombre.ToUpper();

                if (nombre == "BRAND")
                    int.TryParse(valor, out marcaId);

                if (DateTime.TryParse(valor, out DateTime fecha))
                {
                    if (nombre.Contains("DEPARTURE"))
                        fechaDeparture = fecha;

                    if (nombre.Contains("ETA"))
                        fechaEta = fecha;
                }
            }

            // =========================
            // VALIDACIONES
            // =========================
            if (marcaId == 0)
            {
                ModelState.AddModelError("", "Debe seleccionar una marca");
                CargarCombos(empresaId.Value);
                CargarPreguntas();
                return View(new Embarque());
            }

            if (fechaDeparture.HasValue && fechaEta.HasValue && fechaEta < fechaDeparture)
            {
                ModelState.AddModelError("", "ETA no puede ser menor que Departure");
                CargarCombos(empresaId.Value);
                CargarPreguntas();
                return View(new Embarque());
            }

            // =========================
            // RESPUESTAS
            // =========================
            foreach (var p in preguntas)
            {
                var valor = form["pregunta_" + p.Id].ToString();

                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                if (p.Nombre.ToUpper().Contains("TRANSITO"))
                    continue;

                _context.Respuestas.Add(new Respuesta
                {
                    EmbarqueId = embarque.Id,
                    PreguntaId = p.Id,
                    Valor = valor.Trim(),
                    FechaRegistro = DateTime.Now
                });
            }

            // =========================
            // CALCULAR TRANSITO
            // =========================
            if (fechaDeparture.HasValue && fechaEta.HasValue)
            {
                var dias = (fechaEta.Value - fechaDeparture.Value).Days;

                var preguntaTransito = preguntas
                    .FirstOrDefault(x => x.Nombre.ToUpper().Contains("TRANSITO"));

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
            }// =========================
             // 🔥 GUARDAR CONFIRMACIONES
             // =========================
            embarque.DepartureConfirmado = form["departureConfirmado"] == "true";
            embarque.EtaConfirmado = form["etaConfirmado"] == "true";

            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard");
        }


        // =========================
        // =========================
        // EDIT POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormCollection form)
        {
            var embarque = await _context.Embarques.FindAsync(id);

            if (embarque == null)
                return NotFound();

            /// =========================
            // 🔒 BLOQUEO INTELIGENTE
            // =========================
            var respuestasActuales = _context.Respuestas
                .Where(r => r.EmbarqueId == id)
                .ToList();

            var preguntaStatus = _context.Preguntas
                .FirstOrDefault(p => p.Nombre.ToUpper().Contains("STATUS"));

            if (preguntaStatus != null)
            {
                var statusActual = respuestasActuales
                    .FirstOrDefault(r => r.PreguntaId == preguntaStatus.Id)?.Valor?.ToLower();

                var statusNuevo = form["pregunta_" + preguntaStatus.Id].ToString()?.ToLower();

                // 🔥 SI YA ESTABA CLOSED → NO TOCAR NADA
                if (statusActual == "closed")
                {
                    TempData["Error"] = "El embarque ya está cerrado y no puede modificarse.";
                    return RedirectToAction("Dashboard");
                }

                // 🔥 SI EL USUARIO LO CAMBIÓ A CLOSED → GUARDAR Y BLOQUEAR FUTURO
                if (statusNuevo == "closed")
                {
                    // Aquí NO bloqueas ahora, solo dejas que guarde
                    // El siguiente intento de edición ya quedará bloqueado arriba
                }
            }
            // =========================
            // 🔥 AUDITORÍA DETALLADA
            // =========================
            var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema";
            var preguntas = _context.Preguntas.Where(x => x.Activo).ToList();

            foreach (var p in preguntas)
            {
                var nuevoValor = form["pregunta_" + p.Id].ToString()?.Trim();

                var actual = respuestasActuales
                    .FirstOrDefault(r => r.PreguntaId == p.Id);

                var valorAnterior = actual?.Valor?.Trim();

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

            // =========================
            // MODELOS
            // =========================
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

            // =========================
            // RESPUESTAS
            // =========================
            _context.Respuestas.RemoveRange(
                _context.Respuestas.Where(r => r.EmbarqueId == id)
            );

            DateTime? fechaDeparture = null;
            DateTime? fechaEta = null;

            foreach (var p in preguntas)
            {
                var valor = form["pregunta_" + p.Id].ToString();

                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                var nombre = p.Nombre.ToUpper();

                if (DateTime.TryParse(valor, out DateTime fecha))
                {
                    if (nombre.Contains("DEPARTURE"))
                        fechaDeparture = fecha;

                    if (nombre.Contains("ETA"))
                        fechaEta = fecha;
                }
            }

            foreach (var p in preguntas)
            {
                var valor = form["pregunta_" + p.Id].ToString();

                if (string.IsNullOrWhiteSpace(valor))
                    continue;

                if (p.Nombre.ToUpper().Contains("TRANSITO"))
                    continue;

                _context.Respuestas.Add(new Respuesta
                {
                    EmbarqueId = id,
                    PreguntaId = p.Id,
                    Valor = valor.Trim(),
                    FechaRegistro = DateTime.Now
                });
            }

            // =========================
            // CALCULAR TRANSITO
            // =========================
            if (fechaDeparture.HasValue && fechaEta.HasValue)
            {
                var dias = (fechaEta.Value - fechaDeparture.Value).Days;

                var preguntaTransito = preguntas
                    .FirstOrDefault(x => x.Nombre.ToUpper().Contains("TRANSITO"));

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

            // =========================
            // AUDITORÍA GENERAL
            // =========================
            embarque.ModificadoPor = usuarioNombre;
            embarque.FechaModificacion = DateTime.Now;
            // =========================
            // 🔥 GUARDAR CONFIRMACIONES (FALTABA AQUÍ)
            // =========================
            embarque.DepartureConfirmado = form["departureConfirmado"] == "true";
            embarque.EtaConfirmado = form["etaConfirmado"] == "true";

            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard");
        }        // =========================
        // DETAILS
        // =========================
        public async Task<IActionResult> Details(int id)
        {
            var preguntas = await _context.Preguntas
    .OrderBy(p => p.Orden)
    .ToListAsync();

            ViewBag.Preguntas = preguntas;
            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var embarque = await _context.Embarques
                .Include(e => e.Empresa)

                // 🔥 AQUÍ ESTÁ LA CORRECCIÓN CLAVE
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
            var embarque = await _context.Embarques.FindAsync(id);

            if (embarque != null)
            {
                // 🔥 BORRAR RELACIONES PRIMERO
                _context.EmbarqueModelos.RemoveRange(
                    _context.EmbarqueModelos.Where(x => x.EmbarqueId == id)
                );

                _context.Respuestas.RemoveRange(
                    _context.Respuestas.Where(x => x.EmbarqueId == id)
                );

                _context.Embarques.Remove(embarque);
                await _context.SaveChangesAsync();
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