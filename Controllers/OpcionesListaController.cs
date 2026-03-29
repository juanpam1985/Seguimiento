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

        // ============================
        // CARGAR PREGUNTAS
        // ============================
        private void CargarPreguntas(object selected = null)
        {
            ViewBag.PreguntaId = _context.Preguntas
                .Where(p => p.Activo)
                .OrderBy(p => p.Orden)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),              // 🔥 SE GUARDA
                    Text = p.Id + " - " + p.Nombre        // 🔥 SE MUESTRA
                })
                .ToList();
        }

        // ============================
        // INDEX
        // ============================
        public async Task<IActionResult> Index()
        {
            var lista = _context.OpcionesLista
                .Include(o => o.Pregunta)
                .OrderBy(o => o.Pregunta.Orden)
                .ThenBy(o => o.Orden);

            return View(await lista.ToListAsync());
        }

        // ============================
        // CREATE GET
        // ============================
        public IActionResult Create()
        {
            CargarPreguntas();
            return View();
        }

        // ============================
        // CREATE POST (ÚNICO)
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OpcionLista opcionLista)
        {
            // 🔥 CLAVE
            ModelState.Remove("Pregunta");

            if (opcionLista.PreguntaId == null)
            {
                ModelState.AddModelError("PreguntaId", "Seleccione una pregunta");
            }

            if (string.IsNullOrWhiteSpace(opcionLista.Valor))
            {
                ModelState.AddModelError("Valor", "Ingrese un valor");
            }

            if (!ModelState.IsValid)
            {
                CargarPreguntas(opcionLista.PreguntaId);
                return View(opcionLista);
            }

            _context.OpcionesLista.Add(opcionLista);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        // ============================
        // EDIT
        // ============================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var opcionLista = await _context.OpcionesLista.FindAsync(id);

            if (opcionLista == null)
                return NotFound();

            CargarPreguntas(opcionLista.PreguntaId);
            return View(opcionLista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OpcionLista opcionLista)
        {
            if (id != opcionLista.Id)
                return NotFound();

            try
            {
                opcionLista.Activo = Request.Form["Activo"] == "true";

                _context.Update(opcionLista);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                CargarPreguntas(opcionLista.PreguntaId);
                return View(opcionLista);
            }
        }

        // ============================
        // DELETE
        // ============================
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
    }
}