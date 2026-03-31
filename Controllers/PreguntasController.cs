using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;
using Seguimiento.Helpers;

namespace Seguimiento.Controllers
{
    public class PreguntasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PreguntasController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? RolIdSesion()
        {
            return HttpContext.Session.GetInt32("RolId");
        }

        private bool Puede(string accion)
        {
            return PermisoHelper.TienePermiso(_context, RolIdSesion(), "PREGUNTAS", accion);
        }

        // ===============================
        // LISTA DE PREGUNTAS
        // ===============================
        public async Task<IActionResult> Index()
        {
            if (!Puede("VER"))
                return RedirectToAction("Dashboard", "Embarques");

            var preguntas = _context.Preguntas
                .AsNoTracking()
                .Include(p => p.Modelo)
                    .ThenInclude(m => m.Marca)
                        .ThenInclude(m => m.MarcaEmpresas)
                            .ThenInclude(me => me.Empresa);

            return View(await preguntas.ToListAsync());
        }

        // ===============================
        // DETAILS
        // ===============================
        public async Task<IActionResult> Details(int? id)
        {
            if (!Puede("VER"))
                return RedirectToAction("Dashboard", "Embarques");

            if (id == null)
                return NotFound();

            var pregunta = await _context.Preguntas
                .AsNoTracking()
                .Include(p => p.Modelo)
                    .ThenInclude(m => m.Marca)
                        .ThenInclude(m => m.MarcaEmpresas)
                            .ThenInclude(me => me.Empresa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (pregunta == null)
                return NotFound();

            return View(pregunta);
        }

        // ===============================
        // CREATE GET
        // ===============================
        public IActionResult Create()
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            ViewData["ModeloId"] = new SelectList(
                _context.Modelos
                    .AsNoTracking()
                    .Include(m => m.Marca)
                    .OrderBy(m => m.Nombre),
                "Id",
                "Nombre"
            );

            return View();
        }

        // ===============================
        // CREATE POST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ModeloId,Nombre,TipoCampoId,Orden,Activo")] Pregunta pregunta)
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            if (string.IsNullOrWhiteSpace(pregunta.Nombre))
                ModelState.AddModelError("Nombre", "El nombre es obligatorio.");

            if (pregunta.Orden <= 0)
                ModelState.AddModelError("Orden", "El orden debe ser mayor a 0.");

            if (ModelState.IsValid)
            {
                _context.Preguntas.Add(pregunta);
                await _context.SaveChangesAsync();

                TempData["OK"] = "Pregunta creada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["ModeloId"] = new SelectList(
                _context.Modelos
                    .AsNoTracking()
                    .Include(m => m.Marca)
                    .OrderBy(m => m.Nombre),
                "Id",
                "Nombre",
                pregunta.ModeloId
            );

            return View(pregunta);
        }

        // ===============================
        // EDIT GET
        // ===============================
        public async Task<IActionResult> Edit(int? id)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id == null)
                return NotFound();

            var pregunta = await _context.Preguntas.FindAsync(id);

            if (pregunta == null)
                return NotFound();

            ViewData["ModeloId"] = new SelectList(
                _context.Modelos
                    .AsNoTracking()
                    .OrderBy(m => m.Nombre),
                "Id",
                "Nombre",
                pregunta.ModeloId
            );

            return View(pregunta);
        }

        // ===============================
        // EDIT POST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ModeloId,Nombre,TipoCampoId,Orden,Activo")] Pregunta pregunta)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id != pregunta.Id)
                return NotFound();

            if (string.IsNullOrWhiteSpace(pregunta.Nombre))
                ModelState.AddModelError("Nombre", "El nombre es obligatorio.");

            if (pregunta.Orden <= 0)
                ModelState.AddModelError("Orden", "El orden debe ser mayor a 0.");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pregunta);
                    await _context.SaveChangesAsync();

                    TempData["OK"] = "Pregunta actualizada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PreguntaExists(pregunta.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            ViewData["ModeloId"] = new SelectList(
                _context.Modelos
                    .AsNoTracking()
                    .OrderBy(m => m.Nombre),
                "Id",
                "Nombre",
                pregunta.ModeloId
            );

            return View(pregunta);
        }

        // ===============================
        // DELETE
        // ===============================
        public async Task<IActionResult> Delete(int? id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction(nameof(Index));

            if (id == null)
                return NotFound();

            var pregunta = await _context.Preguntas
                .AsNoTracking()
                .Include(p => p.Modelo)
                    .ThenInclude(m => m.Marca)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (pregunta == null)
                return NotFound();

            return View(pregunta);
        }

        // ===============================
        // DELETE CONFIRMED
        // ===============================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction(nameof(Index));

            var pregunta = await _context.Preguntas.FindAsync(id);

            if (pregunta != null)
                _context.Preguntas.Remove(pregunta);

            await _context.SaveChangesAsync();

            TempData["OK"] = "Pregunta eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        private bool PreguntaExists(int id)
        {
            return _context.Preguntas.Any(e => e.Id == id);
        }
    }
}