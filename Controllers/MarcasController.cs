using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;

namespace Seguimiento.Controllers
{
    public class MarcasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MarcasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // LISTADO DE MARCAS
        // ===============================
        public async Task<IActionResult> Index()
        {
            var marcas = _context.Marcas
                .Include(m => m.MarcaEmpresas)
                .ThenInclude(me => me.Empresa);

            return View(await marcas.ToListAsync());
        }

        // ===============================
        // DETAILS
        // ===============================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var marca = await _context.Marcas
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
            ViewBag.Empresas = _context.Empresas
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
            // 🔍 VALIDAR DUPLICADO (marca + empresa)
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

                // 🔥 GUARDAR RELACIONES
                foreach (var empresaId in empresasSeleccionadas)
                {
                    _context.MarcaEmpresas.Add(new MarcaEmpresa
                    {
                        MarcaId = marca.Id,
                        EmpresaId = empresaId
                    });
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Empresas = _context.Empresas.Where(x => x.Activo).ToList();
            return View(marca);
        }

        // ===============================
        // EDIT GET
        // ===============================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var marca = await _context.Marcas
                .Include(m => m.MarcaEmpresas)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marca == null)
                return NotFound();

            ViewBag.Empresas = _context.Empresas.Where(x => x.Activo).ToList();

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
            if (id != marca.Id)
                return NotFound();

            // 🔍 VALIDAR DUPLICADO
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
                _context.Update(marca);
                await _context.SaveChangesAsync();

                // 🔥 ELIMINAR RELACIONES
                var relaciones = _context.MarcaEmpresas.Where(x => x.MarcaId == id);
                _context.MarcaEmpresas.RemoveRange(relaciones);

                // 🔥 INSERTAR NUEVAS
                foreach (var empresaId in empresasSeleccionadas)
                {
                    _context.MarcaEmpresas.Add(new MarcaEmpresa
                    {
                        MarcaId = marca.Id,
                        EmpresaId = empresaId
                    });
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Empresas = _context.Empresas.Where(x => x.Activo).ToList();
            return View(marca);
        }

        // ===============================
        // DELETE
        // ===============================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var marca = await _context.Marcas
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
            var marca = await _context.Marcas.FindAsync(id);

            if (marca != null)
                _context.Marcas.Remove(marca);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool MarcaExists(int id)
        {
            return _context.Marcas.Any(e => e.Id == id);
        }
    }
}