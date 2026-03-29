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
    public class ModelosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ModelosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // INDEX
        // ===============================
        public IActionResult Index()
        {
            var modelos = _context.Modelos
                .Include(m => m.Marca)
                .Include(m => m.TipoModelo) // 🔥 NUEVO
                .ToList();

            return View(modelos);
        }

        // ===============================
        // DETAILS
        // ===============================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var modelo = await _context.Modelos
                .Include(m => m.Marca)
                .Include(m => m.TipoModelo) // 🔥 NUEVO
                .FirstOrDefaultAsync(m => m.Id == id);

            if (modelo == null)
                return NotFound();

            return View(modelo);
        }

        // ===============================
        // CREATE GET
        // ===============================
        public IActionResult Create()
        {
            CargarMarcas();
            CargarTiposModelo(); // 🔥 NUEVO
            return View();
        }

        // ===============================
        // CREATE POST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,MarcaId,Nombre,Activo,TipoModeloId")] Modelo modelo)
        {
            if (modelo.MarcaId == 0)
                ModelState.AddModelError("MarcaId", "Debe seleccionar una marca");

            if (modelo.TipoModeloId == null)
                ModelState.AddModelError("TipoModeloId", "Debe seleccionar un tipo");

            if (string.IsNullOrWhiteSpace(modelo.Nombre))
                ModelState.AddModelError("Nombre", "El nombre es obligatorio");

            if (!ModelState.IsValid)
            {
                CargarMarcas(modelo.MarcaId);
                CargarTiposModelo(modelo.TipoModeloId);
                return View(modelo);
            }

            try
            {
                _context.Modelos.Add(modelo);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);

                CargarMarcas(modelo.MarcaId);
                CargarTiposModelo(modelo.TipoModeloId);
                return View(modelo);
            }
        }

        // ===============================
        // EDIT GET
        // ===============================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var modelo = await _context.Modelos.FindAsync(id);

            if (modelo == null)
                return NotFound();

            CargarMarcas(modelo.MarcaId);
            CargarTiposModelo(modelo.TipoModeloId);

            return View(modelo);
        }

        // ===============================
        // EDIT POST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MarcaId,Nombre,Activo,TipoModeloId")] Modelo modelo)
        {
            if (id != modelo.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(modelo);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ModeloExists(modelo.Id))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            CargarMarcas(modelo.MarcaId);
            CargarTiposModelo(modelo.TipoModeloId);

            return View(modelo);
        }

        // ===============================
        // API MODELOS POR MARCA
        // ===============================
        [HttpGet]
        public IActionResult ObtenerPorMarca(int marcaId)
        {
            var modelos = _context.Modelos
                .Where(m => m.MarcaId == marcaId && m.Activo)
                .Select(m => new
                {
                    id = m.Id,
                    nombre = m.Nombre
                })
                .OrderBy(m => m.nombre)
                .ToList();

            return Json(modelos);
        }

        // ===============================
        // DELETE
        // ===============================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var modelo = await _context.Modelos
                .Include(m => m.Marca)
                .Include(m => m.TipoModelo) // 🔥 NUEVO
                .FirstOrDefaultAsync(m => m.Id == id);

            if (modelo == null)
                return NotFound();

            return View(modelo);
        }

        // ===============================
        // DELETE CONFIRMED
        // ===============================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var modelo = await _context.Modelos.FindAsync(id);

            if (modelo != null)
                _context.Modelos.Remove(modelo);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool ModeloExists(int id)
        {
            return _context.Modelos.Any(e => e.Id == id);
        }

        // ===============================
        // MÉTODOS REUTILIZABLES
        // ===============================
        private void CargarMarcas(object selected = null)
        {
            ViewData["MarcaId"] = new SelectList(
                _context.Marcas
                    .Where(m => m.Activo)
                    .OrderBy(m => m.Nombre),
                "Id",
                "Nombre",
                selected
            );
        }

        private void CargarTiposModelo(object selected = null)
        {
            ViewData["TipoModeloId"] = new SelectList(
                _context.TipoModelo
                    .Where(t => t.Activo)
                    .OrderBy(t => t.Nombre),
                "Id",
                "Nombre",
                selected
            );
        }
    }
}