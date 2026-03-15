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

        // GET: Modelos
        public IActionResult Index()
        {
            var modelos = _context.Modelos
                .Include(m => m.Marca)
                .ToList();

            return View(modelos);
        }

        // GET: Modelos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var modelo = await _context.Modelos
                .Include(m => m.Marca)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (modelo == null)
            {
                return NotFound();
            }

            return View(modelo);
        }

        // GET: Modelos/Create
        public IActionResult Create()
        {
            ViewData["MarcaId"] = new SelectList(_context.Marcas, "Id", "Id");
            return View();
        }

        // POST: Modelos/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,MarcaId,Nombre,Activo")] Modelo modelo)
        {
            if (ModelState.IsValid)
            {
                _context.Add(modelo);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MarcaId"] = new SelectList(_context.Marcas, "Id", "Id", modelo.MarcaId);
            return View(modelo);
        }

        // GET: Modelos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var modelo = await _context.Modelos.FindAsync(id);
            if (modelo == null)
            {
                return NotFound();
            }
            ViewData["MarcaId"] = new SelectList(_context.Marcas, "Id", "Id", modelo.MarcaId);
            return View(modelo);
        }

        // POST: Modelos/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MarcaId,Nombre,Activo")] Modelo modelo)
        {
            if (id != modelo.Id)
            {
                return NotFound();
            }

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
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["MarcaId"] = new SelectList(_context.Marcas, "Id", "Id", modelo.MarcaId);
            return View(modelo);
        }

        //MEJORA CONSULTA MODELO SEGUN LA MARCA
        [HttpGet]
        public IActionResult ObtenerPorMarca(int marcaId)
        {
            var modelos = _context.Modelos
                .Where(m => m.MarcaId == marcaId && m.Activo == true)
                .Select(m => new
                {
                    id = m.Id,
                    nombre = m.Nombre
                })
                .OrderBy(m => m.nombre)
                .ToList();

            return Json(modelos);
        }


        // GET: Modelos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var modelo = await _context.Modelos
                .Include(m => m.Marca)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (modelo == null)
            {
                return NotFound();
            }

            return View(modelo);
        }

        // POST: Modelos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var modelo = await _context.Modelos.FindAsync(id);
            if (modelo != null)
            {
                _context.Modelos.Remove(modelo);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ModeloExists(int id)
        {
            return _context.Modelos.Any(e => e.Id == id);
        }
    }
}
