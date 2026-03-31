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
    public class MarcasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MarcasController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? RolIdSesion()
        {
            return HttpContext.Session.GetInt32("RolId");
        }

        private bool Puede(string accion)
        {
            return PermisoHelper.TienePermiso(_context, RolIdSesion(), "MARCAS", accion);
        }

        // ===============================
        // LISTADO DE MARCAS
        // ===============================
        public async Task<IActionResult> Index()
        {
            if (!Puede("VER"))
                return RedirectToAction("Dashboard", "Embarques");

            var marcas = _context.Marcas
                .AsNoTracking()
                .Include(m => m.MarcaEmpresas)
                .ThenInclude(me => me.Empresa);

            return View(await marcas.ToListAsync());
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

            var marca = await _context.Marcas
                .AsNoTracking()
                .Include(m => m.MarcaEmpresas)
                .ThenInclude(me => me.Empresa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marca == null)
                return NotFound();

            return View(marca);
        }

        // ===============================
        // CREATE GET
        // ===============================
        public IActionResult Create()
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            ViewBag.Empresas = _context.Empresas
                .AsNoTracking()
                .Where(x => x.Activo)
                .ToList();

            return View();
        }

        // ===============================
        // CREATE POST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Marca marca, List<int> empresasSeleccionadas)
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            empresasSeleccionadas = empresasSeleccionadas?
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!empresasSeleccionadas.Any())
            {
                ModelState.AddModelError("", "Debe seleccionar al menos una empresa.");
            }

            foreach (var empresaId in empresasSeleccionadas)
            {
                bool existe = await _context.MarcaEmpresas.AnyAsync(me =>
                    me.EmpresaId == empresaId &&
                    me.Marca.Nombre.ToLower() == marca.Nombre.ToLower()
                );

                if (existe)
                {
                    ModelState.AddModelError("Nombre", "⚠️ Esta marca ya existe en una de las empresas seleccionadas.");
                    break;
                }
            }

            if (ModelState.IsValid)
            {
                _context.Marcas.Add(marca);
                await _context.SaveChangesAsync();

                foreach (var empresaId in empresasSeleccionadas)
                {
                    _context.MarcaEmpresas.Add(new MarcaEmpresa
                    {
                        MarcaId = marca.Id,
                        EmpresaId = empresaId
                    });
                }

                await _context.SaveChangesAsync();

                TempData["OK"] = "Marca creada correctamente.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Empresas = _context.Empresas
                .AsNoTracking()
                .Where(x => x.Activo)
                .ToList();

            ViewBag.EmpresasSeleccionadas = empresasSeleccionadas;

            return View(marca);
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

            var marca = await _context.Marcas
                .Include(m => m.MarcaEmpresas)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marca == null)
                return NotFound();

            ViewBag.Empresas = await _context.Empresas
                .AsNoTracking()
                .Where(x => x.Activo)
                .ToListAsync();

            ViewBag.EmpresasSeleccionadas = marca.MarcaEmpresas
                .Select(me => me.EmpresaId)
                .ToList();

            return View(marca);
        }

        // ===============================
        // EDIT POST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Marca marca, List<int> empresasSeleccionadas)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id != marca.Id)
                return NotFound();

            empresasSeleccionadas = empresasSeleccionadas?
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!empresasSeleccionadas.Any())
            {
                ModelState.AddModelError("", "Debe seleccionar al menos una empresa.");
            }

            foreach (var empresaId in empresasSeleccionadas)
            {
                bool existe = await _context.MarcaEmpresas.AnyAsync(me =>
                    me.EmpresaId == empresaId &&
                    me.Marca.Nombre.ToLower() == marca.Nombre.ToLower() &&
                    me.MarcaId != marca.Id
                );

                if (existe)
                {
                    ModelState.AddModelError("Nombre", "⚠️ Esta marca ya existe en una de las empresas seleccionadas.");
                    break;
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(marca);
                    await _context.SaveChangesAsync();

                    var relaciones = _context.MarcaEmpresas.Where(x => x.MarcaId == id);
                    _context.MarcaEmpresas.RemoveRange(relaciones);
                    await _context.SaveChangesAsync();

                    foreach (var empresaId in empresasSeleccionadas)
                    {
                        _context.MarcaEmpresas.Add(new MarcaEmpresa
                        {
                            MarcaId = marca.Id,
                            EmpresaId = empresaId
                        });
                    }

                    await _context.SaveChangesAsync();

                    TempData["OK"] = "Marca actualizada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MarcaExists(marca.Id))
                        return NotFound();

                    throw;
                }
            }

            ViewBag.Empresas = await _context.Empresas
                .AsNoTracking()
                .Where(x => x.Activo)
                .ToListAsync();

            ViewBag.EmpresasSeleccionadas = empresasSeleccionadas;

            return View(marca);
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

            var marca = await _context.Marcas
                .AsNoTracking()
                .Include(m => m.MarcaEmpresas)
                .ThenInclude(me => me.Empresa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marca == null)
                return NotFound();

            return View(marca);
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

            var relaciones = _context.MarcaEmpresas.Where(x => x.MarcaId == id);
            _context.MarcaEmpresas.RemoveRange(relaciones);

            var marca = await _context.Marcas.FindAsync(id);

            if (marca != null)
                _context.Marcas.Remove(marca);

            await _context.SaveChangesAsync();

            TempData["OK"] = "Marca eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        private bool MarcaExists(int id)
        {
            return _context.Marcas.Any(e => e.Id == id);
        }
    }
}