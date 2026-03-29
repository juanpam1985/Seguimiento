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
        // INDEX
        // ===============================
        public async Task<IActionResult> Index()
        {
            var usuarios = await _context.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.UsuarioEmpresas)
                    .ThenInclude(ue => ue.Empresa)
                .ToListAsync();

            return View(usuarios);
        }

        // ===============================
        // DETAILS
        // ===============================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.UsuarioEmpresas)
                    .ThenInclude(ue => ue.Empresa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (usuario == null) return NotFound();

            return View(usuario);
        }

        // ===============================
        // CREATE GET
        // ===============================
        public IActionResult Create()
        {
            ViewBag.Empresas = _context.Empresas.ToList();
            ViewBag.Roles = _context.Roles.ToList();

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
            ModelState.Remove("Rol"); // 🔥 CLAVE

            if (usuario.RolId == 0)
                ModelState.AddModelError("RolId", "Debe seleccionar un rol");

            if (string.IsNullOrEmpty(usuario.PasswordHash))
                ModelState.AddModelError("PasswordHash", "La contraseña es obligatoria");

            if (ModelState.IsValid)
            {
                usuario.FechaCreacion = DateTime.Now;

                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                var empresasValidas = empresasSeleccionadas?
                    .Where(x => x != 0)
                    .ToList();

                if (empresasValidas != null && empresasValidas.Any())
                {
                    foreach (var empresaId in empresasValidas)
                    {
                        _context.UsuarioEmpresas.Add(new UsuarioEmpresa
                        {
                            UsuarioId = usuario.Id,
                            EmpresaId = empresaId
                        });
                    }

                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Empresas = _context.Empresas.ToList();
            ViewBag.Roles = _context.Roles.ToList();

            return View(usuario);
        }

        // ===============================
        // EDIT GET
        // ===============================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var usuario = await _context.Usuarios
                .Include(u => u.UsuarioEmpresas)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null) return NotFound();

            ViewBag.Empresas = _context.Empresas.ToList();

            ViewBag.EmpresasSeleccionadas = usuario.UsuarioEmpresas
                .Select(x => x.EmpresaId)
                .ToList();

            ViewBag.Roles = _context.Roles.ToList();

            return View(usuario);
        }

        // ===============================
        // EDIT POST
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Usuario usuario)
        {
            if (id != usuario.Id) return NotFound();

            ModelState.Remove("UsuarioEmpresas");
            ModelState.Remove("FechaCreacion");
            ModelState.Remove("Rol"); // 🔥 CLAVE

            var empresasSeleccionadas = Request.Form["empresasSeleccionadas"]
                .Where(x => !string.IsNullOrEmpty(x) && x != "0")
                .Select(x => int.Parse(x))
                .ToList();

            var usuarioDb = await _context.Usuarios
                .Include(u => u.UsuarioEmpresas)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuarioDb == null) return NotFound();

            usuarioDb.Nombre = usuario.Nombre;
            usuarioDb.UsuarioLogin = usuario.UsuarioLogin;
            usuarioDb.Email = usuario.Email;
            usuarioDb.Activo = usuario.Activo;
            usuarioDb.RolId = usuario.RolId;

            var nuevaPassword = Request.Form["NuevaPassword"].ToString();
            if (!string.IsNullOrEmpty(nuevaPassword))
            {
                usuarioDb.PasswordHash = nuevaPassword;
            }

            _context.UsuarioEmpresas.RemoveRange(usuarioDb.UsuarioEmpresas);
            await _context.SaveChangesAsync();

            if (empresasSeleccionadas.Any())
            {
                foreach (var empresaId in empresasSeleccionadas)
                {
                    _context.UsuarioEmpresas.Add(new UsuarioEmpresa
                    {
                        UsuarioId = id,
                        EmpresaId = empresaId
                    });
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // ===============================
        // DELETE
        // ===============================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.UsuarioEmpresas)
                    .ThenInclude(ue => ue.Empresa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (usuario == null) return NotFound();

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
                var relaciones = _context.UsuarioEmpresas
                    .Where(x => x.UsuarioId == id);

                _context.UsuarioEmpresas.RemoveRange(relaciones);
                _context.Usuarios.Remove(usuario);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UsuarioExists(int id)
        {
            return _context.Usuarios.Any(e => e.Id == id);
        }
    }
}