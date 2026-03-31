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

        private const string CAMPO_FECHA_INGRESO_PESOS = "FECHA INGRESO DE PESOS TERMINAL";
        private const string CAMPO_FECHA_SALIDA_AUTORIZADA = "FECHA SALIDA AUTORIZADA";
        private const string CAMPO_DIAS_ENTRE_INGRESO_SALIDA = "DIAS ENTRE INGRESO Y SALIDA";

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
        public async Task<IActionResult> Dashboard(string filtro)
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

            var embarques = await _context.Embarques
                .Include(x => x.Empresa)
                .Include(x => x.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .Include(x => x.EmbarqueModelos)
                .Where(x => x.EmpresaId == empresaId)
                .OrderByDescending(x => x.FechaRegistro)
                .ToListAsync();

            var embarquesOriginal = embarques.ToList();

            var camposObligatorios = new List<string>
            {
                "ETA","TRÁNSITO","HOMOLOGATION","CERTIFICADO 017 INEN","AFIP",
                "INVOICES","BLs","BL","COs","APLICACIONES DE SEGURO","LICENCIA INEN",
                "PACKING","SUBCATEGOR","NOTIFICACIÓN SRI","DESTINO ADUANERO",
                "TIPO DE PAGO","DIAS LIBRES","MRN","PRE-LIQUIDACIÓN","EMISION BL",
                "CAS","PAGO FLETE","ORIGINAL","ESTADO DEL BUQUE"
            };

            if (!string.IsNullOrEmpty(filtro) && filtro != "todos")
            {
                embarques = embarques.Where(e =>
                {
                    var r = e.Respuestas?.ToList() ?? new List<Respuesta>();

                    var status = r.FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains("STATUS"))?.Valor?.ToLower();
                    var departure = r.FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))?.Valor;

                    bool esRetrasado = false;

                    if (DateTime.TryParse(departure, out DateTime depDate) && depDate < DateTime.Now && status != "closed")
                    {
                        foreach (var campo in camposObligatorios)
                        {
                            var val = r.FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains(campo))?.Valor;
                            if (string.IsNullOrWhiteSpace(val))
                            {
                                esRetrasado = true;
                                break;
                            }
                        }
                    }

                    if (filtro == "transito")
                    {
                        var dep = r.FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))?.Valor;
                        bool esRetrasadoLocal = false;

                        if (DateTime.TryParse(dep, out DateTime fechaDep))
                        {
                            if (fechaDep.Date.AddDays(1) < DateTime.Now.Date)
                            {
                                foreach (var campo in camposObligatorios)
                                {
                                    var val = r.FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains(campo))?.Valor;

                                    if (string.IsNullOrWhiteSpace(val))
                                    {
                                        esRetrasadoLocal = true;
                                        break;
                                    }
                                }
                            }
                        }

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

            var total = embarquesOriginal.Count();

            var enTransito = embarquesOriginal.Count(e =>
            {
                var r = e.Respuestas?.ToList() ?? new List<Respuesta>();
                var departure = r.FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))?.Valor;

                bool esRetrasado = false;

                if (DateTime.TryParse(departure, out DateTime depDate))
                {
                    if (depDate.Date.AddDays(1) < DateTime.Now.Date)
                    {
                        foreach (var campo in camposObligatorios)
                        {
                            var val = r.FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains(campo))?.Valor;

                            if (string.IsNullOrWhiteSpace(val))
                            {
                                esRetrasado = true;
                                break;
                            }
                        }
                    }
                }

                return !esRetrasado;
            });

            var entregados = embarquesOriginal.Count(e =>
            {
                var status = e.Respuestas
                    .FirstOrDefault(r => r.Pregunta != null && r.Pregunta.Nombre.ToUpper().Contains("STATUS"))
                    ?.Valor?.ToLower();

                return status == "entregado" || status == "closed";
            });

            var retrasados = embarquesOriginal.Count(e =>
            {
                var r = e.Respuestas?.ToList() ?? new List<Respuesta>();

                var departure = r
                    .FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))
                    ?.Valor;

                if (!DateTime.TryParse(departure, out DateTime depDate))
                    return false;

                if (!(depDate.Date.AddDays(1) < DateTime.Now.Date))
                    return false;

                foreach (var campo in camposObligatorios)
                {
                    var val = r
                        .FirstOrDefault(x => x.Pregunta != null && x.Pregunta.Nombre.ToUpper().Contains(campo))
                        ?.Valor;

                    if (string.IsNullOrWhiteSpace(val))
                        return true;
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

        // =========================
        // CREATE GET
        // =========================
        public IActionResult Create()
        {
            if (!Puede("CREAR"))
                return RedirectToAction("Dashboard");

            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            CargarCombos(empresaId.Value);
            CargarPreguntas();

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

            return View(new Embarque());
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

            CargarCombos(empresaId.Value);
            CargarPreguntas();

            ViewBag.DepartureConfirmado = embarque.Respuestas
                .Any(r => r.PreguntaId == 8 && r.Valor == "CONFIRMADO");

            ViewBag.EtaConfirmado = embarque.Respuestas
                .Any(r => r.PreguntaId == 9 && r.Valor == "CONFIRMADO");

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

            var embarque = new Embarque
            {
                EmpresaId = empresaId.Value,
                FechaRegistro = DateTime.Now,
                CreadoPor = HttpContext.Session.GetString("Usuario"),
                FechaCreacion = DateTime.Now
            };

            _context.Embarques.Add(embarque);
            await _context.SaveChangesAsync();

            _context.AuditoriaEmbarques.Add(new AuditoriaEmbarque
            {
                EmbarqueId = embarque.Id,
                Usuario = HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema",
                Accion = "CREACIÓN",
                Fecha = DateTime.Now,
                Detalle = "Se creó el embarque"
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

            var preguntas = _context.Preguntas
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToList();

            DateTime? fechaDeparture = null;
            DateTime? fechaEta = null;
            DateTime? fechaIngresoPesos = null;
            DateTime? fechaSalidaAutorizada = null;
            int marcaId = 0;

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

            if (fechaIngresoPesos.HasValue && fechaSalidaAutorizada.HasValue && fechaSalidaAutorizada < fechaIngresoPesos)
            {
                ModelState.AddModelError("", "FECHA SALIDA AUTORIZADA no puede ser menor que FECHA INGRESO DE PESOS TERMINAL");
                CargarCombos(empresaId.Value);
                CargarPreguntas();
                return View(new Embarque());
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
                var statusActual = respuestasActuales
                    .FirstOrDefault(r => r.PreguntaId == preguntaStatus.Id)?.Valor?.ToLower();

                var statusNuevo = form["pregunta_" + preguntaStatus.Id].ToString()?.ToLower();

                if (statusActual == "closed")
                {
                    TempData["Error"] = "El embarque ya está cerrado y no puede modificarse.";
                    return RedirectToAction("Dashboard");
                }

                if (statusNuevo == "closed")
                {
                    // Se guarda y desde el siguiente intento quedará bloqueado.
                }
            }

            var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Sistema";
            var preguntas = _context.Preguntas
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToList();

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
                TempData["Error"] = "FECHA SALIDA AUTORIZADA no puede ser menor que FECHA INGRESO DE PESOS TERMINAL.";
                return RedirectToAction("Edit", new { id });
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