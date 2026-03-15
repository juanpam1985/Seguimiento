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

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        
        public IActionResult Login(string usuario, string password)
        {
            var user = _context.Usuarios
                .FirstOrDefault(u => u.UsuarioLogin == usuario
                && u.PasswordHash == password
                && u.Activo == true);

            if (user == null)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos";
                return View();
            }

            HttpContext.Session.SetInt32("UsuarioId", user.Id);
            HttpContext.Session.SetString("UsuarioNombre", user.Nombre);

            return RedirectToAction("SeleccionarEmpresa");
        }



     

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



    [HttpPost]
        public IActionResult SeleccionarEmpresa(int empresaId)
        {

            HttpContext.Session.SetInt32("EmpresaId", empresaId);

            return RedirectToAction("Dashboard", "Embarques");
        }


        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
