using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Services;

namespace Seguimiento.Controllers
{
    public class ReporteController : Controller
    {
        private readonly EmailService _emailService;
        private readonly ApplicationDbContext _context;

        public ReporteController(ApplicationDbContext context)
        {
            _emailService = new EmailService();
            _context = context;
        }

        public async Task<IActionResult> EnviarReporte()
        {
            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var embarques = await _context.Embarques
                .Where(e => e.EmpresaId == empresaId)
                .ToListAsync();

            var respuestas = await _context.Respuestas
                .Include(r => r.Pregunta)
                .Where(r => embarques.Select(e => e.Id).Contains(r.EmbarqueId))
                .ToListAsync();

            int total = embarques.Count;
            int entregados = 0;
            int enTransito = 0;
            int retrasados = 0;

            // 🔥 CAMPOS QUE DEFINEN RETRASO
            var camposObligatorios = new List<string>
            {
                "ETA",
                "TRÁNSITO",
                "HOMOLOGATION",
                "INVOICE",
                "BL",
                "CO",
                "MRN",
                "PAGO"
            };

            foreach (var emb in embarques)
            {
                var r = respuestas.Where(x => x.EmbarqueId == emb.Id).ToList();

                // =========================
                // STATUS
                // =========================
                var status = r
                    .FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains("STATUS"))
                    ?.Valor?.ToLower();

                // =========================
                // DEPARTURE
                // =========================
                var depStr = r
                    .FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains("DEPARTURE"))
                    ?.Valor;

                bool esRetrasado = false;

                if (DateTime.TryParse(depStr, out DateTime fechaDep))
                {
                    if (fechaDep.Date.AddDays(1) < DateTime.Now.Date)
                    {
                        foreach (var campo in camposObligatorios)
                        {
                            var val = r
                                .FirstOrDefault(x => x.Pregunta.Nombre.ToUpper().Contains(campo))
                                ?.Valor;

                            if (string.IsNullOrWhiteSpace(val))
                            {
                                esRetrasado = true;
                                break;
                            }
                        }
                    }
                }

                // =========================
                // CLASIFICACIÓN
                // =========================
                if (status == "closed" || status == "entregado")
                {
                    entregados++;
                }
                else if (esRetrasado)
                {
                    retrasados++;
                }
                else
                {
                    enTransito++;
                }
            }

            string html = GenerarHtml(total, enTransito, entregados, retrasados);

            string[] correos = new string[]
            {
                "sistemas@lozanoasesores.com",
                "coordinacion@lozanoasesores.com",
            };

            await _emailService.EnviarCorreoAsync(
                "📊 Informe Diario de Embarques",
                html,
                correos
            );

            TempData["OK"] = "Informe enviado correctamente";
            return RedirectToAction("Index", "KPI");
        }

        private string GenerarHtml(int total, int transito, int entregados, int retrasados)
        {
            return $@"
            <div style='font-family:Segoe UI;padding:20px'>
                <h2>📊 Informe Diario de Embarques</h2>
                <hr/>

                <table style='width:100%;border-collapse:collapse'>
                    <tr>
                        <th style='background:#3498db;color:white;padding:10px'>Total</th>
                        <th style='background:#f39c12;color:white;padding:10px'>En tránsito</th>
                        <th style='background:#27ae60;color:white;padding:10px'>Entregados</th>
                        <th style='background:#e74c3c;color:white;padding:10px'>Retrasados</th>
                    </tr>
                    <tr style='text-align:center;font-size:18px'>
                        <td>{total}</td>
                        <td>{transito}</td>
                        <td>{entregados}</td>
                        <td>{retrasados}</td>
                    </tr>
                </table>

                <p style='margin-top:20px'>
                    Generado automáticamente por el sistema de seguimiento LozanoAsesores.
                </p>
            </div>
            ";
        }
    }
}