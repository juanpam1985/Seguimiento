using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Seguimiento.Data;
using Seguimiento.Models;
using Microsoft.EntityFrameworkCore;

namespace Seguimiento.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // LOGIN GET
        // =========================
        public IActionResult Login()
        {
            return View();
        }

        // =========================
        // LOGIN POST
        // =========================
        [HttpPost]
        public IActionResult Login(string usuario, string password)
        {
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Ingrese usuario y contraseña";
                return View();
            }

            var user = _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefault(u =>
                    u.UsuarioLogin == usuario &&
                    u.PasswordHash == password &&
                    u.Activo == true
                );

            if (user == null)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos";
                return View();
            }

            // =========================
            // 🔥 SESIÓN USUARIO
            // =========================
            HttpContext.Session.SetInt32("UsuarioId", user.Id);
            HttpContext.Session.SetString("UsuarioNombre", user.Nombre ?? "");

            // =========================
            // 🔥 SESIÓN ROL
            // =========================
            HttpContext.Session.SetInt32("RolId", user.RolId);
            HttpContext.Session.SetString("RolNombre", user.Rol?.Nombre ?? "");

            // =========================
            // 🔥 LIMPIAR EMPRESA
            // =========================
            HttpContext.Session.Remove("Id");
            HttpContext.Session.Remove("EmpresaNombre");

            return RedirectToAction("SeleccionarEmpresa");
        }

        // =========================
        // SELECCIONAR EMPRESA GET
        // =========================
        public IActionResult SeleccionarEmpresa()
        {
            var usuarioId = HttpContext.Session.GetInt32("UsuarioId");

            if (usuarioId == null)
                return RedirectToAction("Login");

            var empresas = _context.UsuarioEmpresas
                .Include(x => x.Empresa)
                .Where(x => x.UsuarioId == usuarioId)
                .Select(x => x.Empresa)
                .ToList();

            return View(empresas);
        }

        // =========================
        // SELECCIONAR EMPRESA POST
        // =========================
        [HttpPost]
        public IActionResult SeleccionarEmpresa(int Id)
        {
            var usuarioId = HttpContext.Session.GetInt32("UsuarioId");

            if (usuarioId == null)
                return RedirectToAction("Login");

            var empresa = _context.Empresas.FirstOrDefault(x => x.Id == Id);

            if (empresa == null)
                return RedirectToAction("SeleccionarEmpresa");

            // VALIDAR QUE ESA EMPRESA LE PERTENEZCA AL USUARIO
            var acceso = _context.UsuarioEmpresas
                .Any(x => x.UsuarioId == usuarioId && x.EmpresaId == Id);

            if (!acceso)
            {
                return RedirectToAction("SeleccionarEmpresa");
            }

            // =========================
            // 🔥 SESIÓN EMPRESA
            // =========================
            HttpContext.Session.SetInt32("Id", empresa.Id);
            HttpContext.Session.SetString("EmpresaNombre", empresa.Nombre);

            return RedirectToAction("Dashboard", "Embarques");
        }

        // =========================
        // 🔒 VALIDAR SESIÓN (UTILIDAD)
        // =========================
        private bool SesionActiva()
        {
            return HttpContext.Session.GetInt32("UsuarioId") != null;
        }

        // =========================
        // 🔒 VALIDAR ROL ADMIN (UTILIDAD)
        // =========================
        private bool EsAdmin()
        {
            var rolId = HttpContext.Session.GetInt32("RolId");
            return rolId == 1; // asume 1 = admin
        }

        // =========================
        // LOGOUT
        // =========================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}