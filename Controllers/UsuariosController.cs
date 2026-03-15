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
    public class UsuariosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsuariosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // LISTA DE USUARIOS
        // ===============================
        public async Task<IActionResult> Index()
        {
            return View(await _context.Usuarios.ToListAsync());
        }

        // ===============================
        // DETAILS
        // ===============================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(m => m.Id == id);

            if (usuario == null)
                return NotFound();

            return View(usuario);
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
        public async Task<IActionResult> Create(Usuario usuario, List<int> empresasSeleccionadas)
        {
            ModelState.Remove("FechaCreacion");
            ModelState.Remove("UsuarioEmpresas");

            if (ModelState.IsValid)
            {
                usuario.FechaCreacion = DateTime.Now;

                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                if (empresasSeleccionadas != null)
                {
                    foreach (var empresaId in empresasSeleccionadas)
                    {
                        var relacion = new UsuarioEmpresa
                        {
                            UsuarioId = usuario.Id,
                            EmpresaId = empresaId
                        };

                        _context.UsuarioEmpresas.Add(relacion);
                    }

                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Empresas = _context.Empresas
                .Where(x => x.Activo)
                .ToList();

            return View(usuario);
        }

        // ===============================
        // EDIT GET
        // ===============================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
                return NotFound();

            return View(usuario);
        }

        // ===============================
        // EDIT POST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Usuario usuario)
        {
            if (id != usuario.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(usuario);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UsuarioExists(usuario.Id))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(usuario);
        }

        // ===============================
        // DELETE
        // ===============================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(m => m.Id == id);

            if (usuario == null)
                return NotFound();

            return View(usuario);
        }

        // ===============================
        // DELETE CONFIRMED
        // ===============================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario != null)
            {
                _context.Usuarios.Remove(usuario);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // EXISTE USUARIO
        // ===============================
        private bool UsuarioExists(int id)
        {
            return _context.Usuarios.Any(e => e.Id == id);
        }
    }
}
