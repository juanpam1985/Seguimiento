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
    public class UsuariosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsuariosController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? RolIdSesion()
        {
            return HttpContext.Session.GetInt32("RolId");
        }

        private bool Puede(string accion)
        {
            return PermisoHelper.TienePermiso(_context, RolIdSesion(), "USUARIOS", accion);
        }

        public async Task<IActionResult> Index()
        {
            if (!Puede("VER"))
                return RedirectToAction("Dashboard", "Embarques");

            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .Include(u => u.UsuarioEmpresas)
                    .ThenInclude(ue => ue.Empresa)
                .ToListAsync();

            return View(usuarios);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (!Puede("VER"))
                return RedirectToAction("Dashboard", "Embarques");

            if (id == null)
                return NotFound();

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .Include(u => u.UsuarioEmpresas)
                    .ThenInclude(ue => ue.Empresa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (usuario == null)
                return NotFound();

            return View(usuario);
        }

        public IActionResult Create()
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            ViewBag.Empresas = _context.Empresas
                .AsNoTracking()
                .ToList();

            ViewBag.Roles = _context.Roles
                .AsNoTracking()
                .Where(r => r.Activo)
                .ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Usuario usuario, List<int> empresasSeleccionadas)
        {
            if (!Puede("CREAR"))
                return RedirectToAction(nameof(Index));

            ModelState.Remove("FechaCreacion");
            ModelState.Remove("UsuarioEmpresas");
            ModelState.Remove("Rol");

            if (usuario.RolId == 0)
                ModelState.AddModelError("RolId", "Debe seleccionar un rol.");

            if (string.IsNullOrWhiteSpace(usuario.PasswordHash))
                ModelState.AddModelError("PasswordHash", "La contraseña es obligatoria.");

            if (ModelState.IsValid)
            {
                usuario.FechaCreacion = DateTime.Now;

                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                var empresasValidas = empresasSeleccionadas?
                    .Where(x => x > 0)
                    .Distinct()
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

                TempData["OK"] = "Usuario creado correctamente.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Empresas = _context.Empresas
                .AsNoTracking()
                .ToList();

            ViewBag.Roles = _context.Roles
                .AsNoTracking()
                .Where(r => r.Activo)
                .ToList();

            ViewBag.EmpresasSeleccionadas = empresasSeleccionadas?
                .Where(x => x > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            return View(usuario);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id == null)
                return NotFound();

            var usuario = await _context.Usuarios
                .Include(u => u.UsuarioEmpresas)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
                return NotFound();

            ViewBag.Empresas = await _context.Empresas
                .AsNoTracking()
                .ToListAsync();

            ViewBag.EmpresasSeleccionadas = usuario.UsuarioEmpresas
                .Select(x => x.EmpresaId)
                .ToList();

            ViewBag.Roles = await _context.Roles
                .AsNoTracking()
                .Where(r => r.Activo)
                .ToListAsync();

            return View(usuario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Usuario usuario)
        {
            if (!Puede("EDITAR"))
                return RedirectToAction(nameof(Index));

            if (id != usuario.Id)
                return NotFound();

            ModelState.Remove("UsuarioEmpresas");
            ModelState.Remove("FechaCreacion");
            ModelState.Remove("Rol");
            ModelState.Remove("PasswordHash");

            var empresasSeleccionadas = Request.Form["empresasSeleccionadas"]
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "0")
                .Select(x => int.TryParse(x, out int valor) ? valor : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var usuarioDb = await _context.Usuarios
                .Include(u => u.UsuarioEmpresas)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuarioDb == null)
                return NotFound();

            if (usuario.RolId == 0)
                ModelState.AddModelError("RolId", "Debe seleccionar un rol.");

            if (!ModelState.IsValid)
            {
                ViewBag.Empresas = await _context.Empresas
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.EmpresasSeleccionadas = empresasSeleccionadas;

                ViewBag.Roles = await _context.Roles
                    .AsNoTracking()
                    .Where(r => r.Activo)
                    .ToListAsync();

                return View(usuario);
            }

            usuarioDb.Nombre = usuario.Nombre;
            usuarioDb.UsuarioLogin = usuario.UsuarioLogin;
            usuarioDb.Email = usuario.Email;
            usuarioDb.Activo = usuario.Activo;
            usuarioDb.RolId = usuario.RolId;

            var nuevaPassword = Request.Form["NuevaPassword"].ToString();
            if (!string.IsNullOrWhiteSpace(nuevaPassword))
            {
                usuarioDb.PasswordHash = nuevaPassword;
            }

            _context.UsuarioEmpresas.RemoveRange(usuarioDb.UsuarioEmpresas);

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
            }

            await _context.SaveChangesAsync();

            TempData["OK"] = "Usuario actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction(nameof(Index));

            if (id == null)
                return NotFound();

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .Include(u => u.UsuarioEmpresas)
                    .ThenInclude(ue => ue.Empresa)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (usuario == null)
                return NotFound();

            return View(usuario);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Puede("ELIMINAR"))
                return RedirectToAction(nameof(Index));

            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario != null)
            {
                var relaciones = _context.UsuarioEmpresas
                    .Where(x => x.UsuarioId == id);

                _context.UsuarioEmpresas.RemoveRange(relaciones);
                _context.Usuarios.Remove(usuario);

                await _context.SaveChangesAsync();
                TempData["OK"] = "Usuario eliminado correctamente.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}