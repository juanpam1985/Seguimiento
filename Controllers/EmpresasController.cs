using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;
using Seguimiento.Helpers;

namespace Seguimiento.Controllers
{
    public class EmpresasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmpresasController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? RolIdSesion()
        {
            return HttpContext.Session.GetInt32("RolId");
        }

        private bool Puede(string accion)
        {
            return PermisoHelper.TienePermiso(_context, RolIdSesion(), "EMPRESAS", accion);
        }

        // GET: Empresas
        public async Task<IActionResult> Index()
        {
            if (!Puede("VER"))
                return RedirectToAction("Dashboard", "Embarques");

            var empresas = await _context.Empresas
                .AsNoTracking()
                .ToListAsync();

            return View(empresas);
        }

        // GET: Empresas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (!Puede("VER"))
                return RedirectToAction("Dashboard", "Embarques");

            if (id == null)
                return NotFound();

            var empresa = await _context.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (empresa == null)
                return NotFound();

            return View(empresa);
        }

        // GET: Empresas/Create
        public IActionResult Create()
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            return View();
        }

        // POST: Empresas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Activo")] Empresa empresa)
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            if (ModelState.IsValid)
            {
                _context.Add(empresa);
                await _context.SaveChangesAsync();

                TempData["OK"] = "Empresa creada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            return View(empresa);
        }

        // GET: Empresas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id == null)
                return NotFound();

            var empresa = await _context.Empresas.FindAsync(id);

            if (empresa == null)
                return NotFound();

            return View(empresa);
        }

        // POST: Empresas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Activo")] Empresa empresa)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id != empresa.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(empresa);
                    await _context.SaveChangesAsync();

                    TempData["OK"] = "Empresa actualizada correctamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmpresaExists(empresa.Id))
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

            return View(empresa);
        }

        // GET: Empresas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction(nameof(Index));

            if (id == null)
                return NotFound();

            var empresa = await _context.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (empresa == null)
                return NotFound();

            return View(empresa);
        }

        // POST: Empresas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction(nameof(Index));

            var empresa = await _context.Empresas.FindAsync(id);

            if (empresa != null)
            {
                _context.Empresas.Remove(empresa);
                await _context.SaveChangesAsync();

                TempData["OK"] = "Empresa eliminada correctamente.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool EmpresaExists(int id)
        {
            return _context.Empresas.Any(e => e.Id == id);
        }
    }
}