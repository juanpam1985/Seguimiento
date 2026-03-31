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
    public class OpcionesListaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OpcionesListaController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? RolIdSesion()
        {
            return HttpContext.Session.GetInt32("RolId");
        }

        private bool Puede(string accion)
        {
            return PermisoHelper.TienePermiso(_context, RolIdSesion(), "OPCIONES", accion);
        }

        // ============================
        // CARGAR PREGUNTAS
        // ============================
        private void CargarPreguntas(object selected = null)
        {
            ViewBag.PreguntaId = _context.Preguntas
                .AsNoTracking()
                .Where(p => p.Activo)
                .OrderBy(p => p.Orden)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Id + " - " + p.Nombre,
                    Selected = selected != null && p.Id.ToString() == selected.ToString()
                })
                .ToList();
        }

        // ============================
        // INDEX
        // ============================
        public async Task<IActionResult> Index()
        {
            if (!Puede("VER"))
                return RedirectToAction("Dashboard", "Embarques");

            var lista = _context.OpcionesLista
                .AsNoTracking()
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
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            CargarPreguntas();
            return View();
        }

        // ============================
        // CREATE POST
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OpcionLista opcionLista)
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            ModelState.Remove("Pregunta");

            if (opcionLista.PreguntaId == null)
                ModelState.AddModelError("PreguntaId", "Seleccione una pregunta");

            if (string.IsNullOrWhiteSpace(opcionLista.Valor))
                ModelState.AddModelError("Valor", "Ingrese un valor");

            if (!ModelState.IsValid)
            {
                CargarPreguntas(opcionLista.PreguntaId);
                return View(opcionLista);
            }

            _context.OpcionesLista.Add(opcionLista);
            await _context.SaveChangesAsync();

            TempData["OK"] = "Opción creada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ============================
        // EDIT GET
        // ============================
        public async Task<IActionResult> Edit(int? id)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id == null)
                return NotFound();

            var opcionLista = await _context.OpcionesLista.FindAsync(id);

            if (opcionLista == null)
                return NotFound();

            CargarPreguntas(opcionLista.PreguntaId);
            return View(opcionLista);
        }

        // ============================
        // EDIT POST
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OpcionLista opcionLista)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id != opcionLista.Id)
                return NotFound();

            ModelState.Remove("Pregunta");

            if (opcionLista.PreguntaId == null)
                ModelState.AddModelError("PreguntaId", "Seleccione una pregunta");

            if (string.IsNullOrWhiteSpace(opcionLista.Valor))
                ModelState.AddModelError("Valor", "Ingrese un valor");

            if (!ModelState.IsValid)
            {
                CargarPreguntas(opcionLista.PreguntaId);
                return View(opcionLista);
            }

            try
            {
                opcionLista.Activo = Request.Form["Activo"] == "true";

                _context.Update(opcionLista);
                await _context.SaveChangesAsync();

                TempData["OK"] = "Opción actualizada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                ModelState.AddModelError("", "Ocurrió un error al actualizar la opción.");
                CargarPreguntas(opcionLista.PreguntaId);
                return View(opcionLista);
            }
        }

        // ============================
        // DELETE
        // ============================
        public async Task<IActionResult> Delete(int? id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction(nameof(Index));

            if (id == null)
                return NotFound();

            var opcionLista = await _context.OpcionesLista
                .AsNoTracking()
                .Include(o => o.Pregunta)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (opcionLista == null)
                return NotFound();

            return View(opcionLista);
        }

        // ============================
        // DELETE CONFIRMED
        // ============================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction(nameof(Index));

            var opcionLista = await _context.OpcionesLista.FindAsync(id);

            if (opcionLista != null)
                _context.OpcionesLista.Remove(opcionLista);

            await _context.SaveChangesAsync();

            TempData["OK"] = "Opción eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}