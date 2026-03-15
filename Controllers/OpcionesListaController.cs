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
    public class OpcionesListaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OpcionesListaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // LISTADO
        // =====================================================

        public async Task<IActionResult> Index()
        {
            var lista = _context.OpcionesLista
                .Include(o => o.Pregunta)
                .OrderBy(o => o.PreguntaId)
                .ThenBy(o => o.Orden);

            return View(await lista.ToListAsync());
        }

        // =====================================================
        // DETAILS
        // =====================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var opcionLista = await _context.OpcionesLista
                .Include(o => o.Pregunta)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (opcionLista == null)
                return NotFound();

            return View(opcionLista);
        }

        // =====================================================
        // CREATE
        // =====================================================

        public IActionResult Create()
        {
            ViewData["PreguntaId"] = new SelectList(_context.Preguntas, "Id", "Nombre");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,PreguntaId,Valor,Orden,Activo")] OpcionLista opcionLista)
        {
            if (ModelState.IsValid)
            {
                _context.Add(opcionLista);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["PreguntaId"] = new SelectList(_context.Preguntas, "Id", "Nombre", opcionLista.PreguntaId);
            return View(opcionLista);
        }

        // =====================================================
        // EDIT
        // =====================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var opcionLista = await _context.OpcionesLista.FindAsync(id);

            if (opcionLista == null)
                return NotFound();

            ViewData["PreguntaId"] = new SelectList(_context.Preguntas, "Id", "Nombre", opcionLista.PreguntaId);

            return View(opcionLista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PreguntaId,Valor,Orden,Activo")] OpcionLista opcionLista)
        {
            if (id != opcionLista.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(opcionLista);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OpcionListaExists(opcionLista.Id))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["PreguntaId"] = new SelectList(_context.Preguntas, "Id", "Nombre", opcionLista.PreguntaId);
            return View(opcionLista);
        }

        // =====================================================
        // DELETE
        // =====================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var opcionLista = await _context.OpcionesLista
                .Include(o => o.Pregunta)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (opcionLista == null)
                return NotFound();

            return View(opcionLista);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var opcionLista = await _context.OpcionesLista.FindAsync(id);

            if (opcionLista != null)
                _context.OpcionesLista.Remove(opcionLista);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // EXISTE
        // =====================================================

        private bool OpcionListaExists(int id)
        {
            return _context.OpcionesLista.Any(e => e.Id == id);
        }
    }
}
