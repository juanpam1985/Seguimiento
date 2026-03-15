using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Seguimiento.Data;
using Seguimiento.Models;

namespace Seguimiento.Controllers
{
    public class EmbarquesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmbarquesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // LISTADO
        // =========================
        public async Task<IActionResult> Index()
        {
            var empresaId = HttpContext.Session.GetInt32("EmpresaId");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            return View(await _context.Embarques
                .Where(x => x.EmpresaId == empresaId)
                .ToListAsync());
        }

        // =========================
        // DASHBOARD
        // =========================
        public IActionResult Dashboard()
        {
            var empresaId = HttpContext.Session.GetInt32("EmpresaId");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var embarques = _context.Embarques
                .Where(x => x.EmpresaId == empresaId)
                .OrderByDescending(x => x.FechaRegistro)
                .ToList();

            return View(embarques);
        }

        // =========================
        // DETAILS
        // =========================
        public async Task<IActionResult> Details(int? id)
        {
            var empresaId = HttpContext.Session.GetInt32("EmpresaId");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            if (id == null)
                return NotFound();

            var embarque = await _context.Embarques
                .FirstOrDefaultAsync(m => m.Id == id && m.EmpresaId == empresaId);

            if (embarque == null)
                return NotFound();

            return View(embarque);
        }

        // =========================
        // CREATE GET
        // =========================
        public IActionResult Create()
        {
            var empresaId = HttpContext.Session.GetInt32("EmpresaId");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            ViewBag.Marcas = _context.Marcas
                .Where(x => x.Activo == true)
                .OrderBy(x => x.Nombre)
                .ToList();

            return View();
        }

        // =========================
        // CREATE POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Embarque embarque)
        {
            var empresaId = HttpContext.Session.GetInt32("EmpresaId");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                embarque.EmpresaId = empresaId.Value;
                embarque.FechaRegistro = DateTime.Now;

                _context.Embarques.Add(embarque);
                await _context.SaveChangesAsync();

                // =========================
                // GUARDAR MODELOS DINAMICOS
                // =========================

                var modelos = _context.Modelos
                    .Where(x => x.Activo == true)
                    .ToList();

                foreach (var modelo in modelos)
                {
                    var campo = Request.Form["modelo_" + modelo.Id];

                    if (!string.IsNullOrEmpty(campo))
                    {
                        int cantidad = 0;

                        int.TryParse(campo, out cantidad);

                        if (cantidad > 0)
                        {
                            var em = new EmbarqueModelo
                            {
                                EmbarqueId = embarque.Id,
                                Id = modelo.Id,
                                Cantidad = cantidad
                            };

                            _context.EmbarqueModelos.Add(em);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Marcas = _context.Marcas
                .Where(x => x.Activo == true)
                .ToList();

            return View(embarque);
        }

        // =========================
        // EDIT
        // =========================
        public async Task<IActionResult> Edit(int? id)
        {
            var empresaId = HttpContext.Session.GetInt32("EmpresaId");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            if (id == null)
                return NotFound();

            var embarque = await _context.Embarques
                .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId);

            if (embarque == null)
                return NotFound();

            return View(embarque);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Embarque embarque)
        {
            if (id != embarque.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(embarque);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmbarqueExists(embarque.Id))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(embarque);
        }

        // =========================
        // DELETE
        // =========================
        public async Task<IActionResult> Delete(int? id)
        {
            var empresaId = HttpContext.Session.GetInt32("EmpresaId");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            if (id == null)
                return NotFound();

            var embarque = await _context.Embarques
                .FirstOrDefaultAsync(m => m.Id == id && m.EmpresaId == empresaId);

            if (embarque == null)
                return NotFound();

            return View(embarque);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var empresaId = HttpContext.Session.GetInt32("EmpresaId");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var embarque = await _context.Embarques
                .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId);

            if (embarque != null)
                _context.Embarques.Remove(embarque);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool EmbarqueExists(int id)
        {
            return _context.Embarques.Any(e => e.Id == id);
        }
    }
}
