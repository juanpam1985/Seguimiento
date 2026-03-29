using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;

namespace Seguimiento.Controllers
{
    public class PreguntasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PreguntasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // LISTA DE PREGUNTAS
        // ===============================
        public async Task<IActionResult> Index()
        {
            var preguntas = _context.Preguntas
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
            if (id == null)
                return NotFound();

            var pregunta = await _context.Preguntas
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
            ViewData["ModeloId"] = new SelectList(
                _context.Modelos
                    .Include(m => m.Marca),
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
            if (ModelState.IsValid)
            {
                _context.Preguntas.Add(pregunta);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewData["ModeloId"] = new SelectList(
                _context.Modelos,
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
            if (id == null)
                return NotFound();

            var pregunta = await _context.Preguntas.FindAsync(id);

            if (pregunta == null)
                return NotFound();

            ViewData["ModeloId"] = new SelectList(
                _context.Modelos,
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
            if (id != pregunta.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pregunta);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PreguntaExists(pregunta.Id))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["ModeloId"] = new SelectList(
                _context.Modelos,
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
            if (id == null)
                return NotFound();

            var pregunta = await _context.Preguntas
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
            var pregunta = await _context.Preguntas.FindAsync(id);

            if (pregunta != null)
                _context.Preguntas.Remove(pregunta);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool PreguntaExists(int id)
        {
            return _context.Preguntas.Any(e => e.Id == id);
        }
    }
}