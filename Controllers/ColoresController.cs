using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;

namespace Seguimiento.Controllers
{
    public class ColoresController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ColoresController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Colores
        public async Task<IActionResult> Index(string buscar)
        {
            var query = _context.Colores.AsQueryable();

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                buscar = buscar.Trim();

                query = query.Where(c =>
                    c.Codigo.Contains(buscar) ||
                    c.Nombre.Contains(buscar) ||
                    (c.NombreLocal != null && c.NombreLocal.Contains(buscar))
                );
            }

            var lista = await query
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            ViewBag.Buscar = buscar;
            return View(lista);
        }

        // GET: Colores/Create
        public IActionResult Create()
        {
            var model = new Color
            {
                Activo = true,
                FechaCreacion = DateTime.Now
            };

            return View(model);
        }

        // POST: Colores/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Color color)
        {
            if (await _context.Colores.AnyAsync(c => c.Codigo == color.Codigo))
            {
                ModelState.AddModelError("Codigo", "Ya existe un color con ese código.");
            }

            if (ModelState.IsValid)
            {
                color.FechaCreacion = DateTime.Now;
                _context.Add(color);
                await _context.SaveChangesAsync();
                TempData["Ok"] = "Color creado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            return View(color);
        }

        // GET: Colores/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var color = await _context.Colores.FindAsync(id);

            if (color == null)
                return NotFound();

            return View(color);
        }

        // POST: Colores/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Color color)
        {
            if (id != color.Id)
                return NotFound();

            if (await _context.Colores.AnyAsync(c => c.Codigo == color.Codigo && c.Id != color.Id))
            {
                ModelState.AddModelError("Codigo", "Ya existe otro color con ese código.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var colorDb = await _context.Colores.FindAsync(id);

                    if (colorDb == null)
                        return NotFound();

                    colorDb.Codigo = color.Codigo;
                    colorDb.Nombre = color.Nombre;
                    colorDb.NombreLocal = color.NombreLocal;
                    colorDb.Activo = color.Activo;

                    await _context.SaveChangesAsync();

                    TempData["Ok"] = "Color actualizado correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Ocurrió un error al actualizar el color.");
                }
            }

            return View(color);
        }

        // GET: Colores/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var color = await _context.Colores.FirstOrDefaultAsync(c => c.Id == id);

            if (color == null)
                return NotFound();

            return View(color);
        }

        // POST: Colores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var color = await _context.Colores.FindAsync(id);

            if (color == null)
                return NotFound();

            // Eliminación lógica
            color.Activo = false;
            await _context.SaveChangesAsync();

            TempData["Ok"] = "Color desactivado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}