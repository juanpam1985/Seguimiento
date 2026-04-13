using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;
using Seguimiento.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private static string NormalizarStatus(string valor)
        {
            return (valor ?? "").Trim().ToLower();
        }

        private static bool EsCerrado(string status)
        {
            status = NormalizarStatus(status);
            return status == "closed" || status == "cerrado" || status == "entregado";
        }

        private class DetalleKPI
        {
            public int EmbarqueId { get; set; }
            public string Marca { get; set; } = "";
            public string Shipment { get; set; } = "";
            public DateTime? ETA { get; set; }
            public DateTime? Departure { get; set; }
            public string Destino { get; set; } = "";
            public string Naviera { get; set; } = "";
            public int Cantidad { get; set; }
            public string Status { get; set; } = "";
            public bool EsRetrasado { get; set; }
        }

        private class RankingItem
        {
            public string Marca { get; set; } = "";
            public int Total { get; set; }
            public int Retrasos { get; set; }
            public double OTIF { get; set; }
        }

        private class ResumenItem
        {
            public string Marca { get; set; } = "";
            public string Shipment { get; set; } = "";
            public DateTime? ETA { get; set; }
            public DateTime? Departure { get; set; }
            public string Destino { get; set; } = "";
            public string Naviera { get; set; } = "";
            public int Cantidad { get; set; }
            public string Estado { get; set; } = "";
        }

        public async Task<IActionResult> EnviarReporte(DateTime? fechaInicio, DateTime? fechaFin, string marcaFiltro = "")
        {
            var empresaId = HttpContext.Session.GetInt32("Id");

            if (empresaId == null)
                return RedirectToAction("Login", "Account");

            var query = _context.Embarques
                .Where(e => e.EmpresaId == empresaId)
                .Include(e => e.Respuestas)
                    .ThenInclude(r => r.Pregunta)
                .AsQueryable();

            if (fechaInicio.HasValue)
                query = query.Where(e => e.FechaRegistro >= fechaInicio.Value);

            if (fechaFin.HasValue)
                query = query.Where(e => e.FechaRegistro <= fechaFin.Value);

            var lista = await query.ToListAsync();

            var marcasDb = await _context.Marcas
                .AsNoTracking()
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            int total = 0;
            int retrasados = 0;
            int enTransito = 0;
            int entregados = 0;
            int abiertos = 0;
            int cerrados = 0;

            var marcas = new Dictionary<string, (int total, int retrasos)>();

            var camposObligatorios = new List<string>
            {
                "ETA",
                "TRÁNSITO",
                "HOMOLOGATION",
                "CERTIFICADO 017 INEN",
                "AFIP",
                "INVOICES",
                "BL",
                "CO",
                "LICENCIA INEN"
            };

            var detalle = new List<DetalleKPI>();

            foreach (var e in lista)
            {
                var respuestas = e.Respuestas ?? new List<Respuesta>();

                string GetValor(string nombre)
                {
                    return respuestas.FirstOrDefault(r =>
                        r.Pregunta != null &&
                        r.Pregunta.Nombre != null &&
                        r.Pregunta.Nombre.ToUpper().Contains(nombre.ToUpper())
                    )?.Valor ?? "";
                }

                DateTime? GetFecha(string nombre)
                {
                    var val = GetValor(nombre);
                    return DateTime.TryParse(val, out DateTime d) ? d : (DateTime?)null;
                }

                var departure = GetFecha("DEPARTURE");
                var eta = GetFecha("ETA");
                var status = NormalizarStatus(GetValor("STATUS"));
                var shipment = GetValor("SHIPMENT");
                var destino = GetValor("DESTINATION");
                var naviera = GetValor("SHIPPING");
                var cantidadTexto = GetValor("QTY");

                int cantidad = 0;
                int.TryParse(cantidadTexto, out cantidad);

                bool esRetrasado = false;

                if (!EsCerrado(status) &&
                    departure.HasValue &&
                    departure.Value.Date.AddDays(1) < DateTime.Now.Date)
                {
                    foreach (var campo in camposObligatorios)
                    {
                        var val = GetValor(campo);

                        if (string.IsNullOrWhiteSpace(val))
                        {
                            esRetrasado = true;
                            break;
                        }
                    }
                }

                var marcaIdTexto = GetValor("BRAND");
                string marca = "SIN MARCA";

                if (int.TryParse(marcaIdTexto, out int marcaId))
                {
                    marca = marcasDb.FirstOrDefault(m => m.Id == marcaId)?.Nombre ?? "SIN MARCA";
                }

                detalle.Add(new DetalleKPI
                {
                    EmbarqueId = e.Id,
                    Marca = marca,
                    Shipment = shipment,
                    ETA = eta,
                    Departure = departure,
                    Destino = destino,
                    Naviera = naviera,
                    Cantidad = cantidad,
                    Status = status,
                    EsRetrasado = esRetrasado
                });
            }

            if (!string.IsNullOrWhiteSpace(marcaFiltro))
            {
                detalle = detalle
                    .Where(x => x.Marca.Equals(marcaFiltro, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            total = detalle.Count;

            foreach (var e in detalle)
            {
                if (!marcas.ContainsKey(e.Marca))
                    marcas[e.Marca] = (0, 0);

                var marcaActual = marcas[e.Marca];
                marcas[e.Marca] = (marcaActual.total + 1, marcaActual.retrasos);

                if (!EsCerrado(e.Status))
                    abiertos++;

                if (EsCerrado(e.Status))
                {
                    cerrados++;
                    entregados++;
                }

                if (e.EsRetrasado)
                {
                    retrasados++;

                    var actual = marcas[e.Marca];
                    marcas[e.Marca] = (actual.total, actual.retrasos + 1);
                }

                if (!EsCerrado(e.Status))
                    enTransito++;
            }

            double otif = total > 0 ? ((double)(total - retrasados) / total) * 100 : 0;

            var ranking = marcas
                .Select(m => new RankingItem
                {
                    Marca = m.Key,
                    Total = m.Value.total,
                    Retrasos = m.Value.retrasos,
                    OTIF = m.Value.total > 0
                        ? ((double)(m.Value.total - m.Value.retrasos) / m.Value.total) * 100
                        : 0
                })
                .OrderByDescending(x => x.OTIF)
                .ToList();

            var resumenPorMarca = detalle
                .OrderBy(x => x.ETA ?? DateTime.MaxValue)
                .ThenBy(x => x.Marca)
                .Select(x => new ResumenItem
                {
                    Marca = x.Marca,
                    Shipment = x.Shipment,
                    ETA = x.ETA,
                    Departure = x.Departure,
                    Destino = x.Destino,
                    Naviera = x.Naviera,
                    Cantidad = x.Cantidad,
                    Estado = EsCerrado(x.Status) ? "Entregado" : x.EsRetrasado ? "Retrasado" : "En tránsito"
                })
                .ToList();

            string html = GenerarHtml(
                total,
                enTransito,
                entregados,
                retrasados,
                cerrados,
                (int)Math.Round(otif, 0),
                ranking,
                resumenPorMarca,
                fechaInicio,
                fechaFin,
                marcaFiltro
            );

            string[] correos = new string[]
            {
                "sistemas@lozanoasesores.com",
                "coordinacion@lozanoasesores.com"
            };

            await _emailService.EnviarCorreoAsync(
                "📊 Informe Diario de Embarques",
                html,
                correos
            );

            TempData["OK"] = "Informe enviado correctamente";

            return RedirectToAction("Index", "KPI", new
            {
                fechaInicio = fechaInicio?.ToString("yyyy-MM-dd"),
                fechaFin = fechaFin?.ToString("yyyy-MM-dd"),
                marcaFiltro = marcaFiltro
            });
        }

        private string GenerarHtml(
            int total,
            int enTransito,
            int entregados,
            int retrasados,
            int cerrados,
            int otif,
            List<RankingItem> ranking,
            List<ResumenItem> resumenPorMarca,
            DateTime? fechaInicio,
            DateTime? fechaFin,
            string marcaFiltro)
        {
            var sb = new StringBuilder();

            string rango = "Todos los registros";
            if (fechaInicio.HasValue || fechaFin.HasValue)
            {
                string fi = fechaInicio.HasValue ? fechaInicio.Value.ToString("dd/MM/yyyy") : "Sin inicio";
                string ff = fechaFin.HasValue ? fechaFin.Value.ToString("dd/MM/yyyy") : "Sin fin";
                rango = fi + " - " + ff;
            }

            string marcaTexto = string.IsNullOrWhiteSpace(marcaFiltro) ? "Todas las marcas" : marcaFiltro;

            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Informe Diario de Embarques</title>
</head>
<body style='margin:0;padding:0;background:#f4f6fb;font-family:Segoe UI,Arial,sans-serif;color:#111827;'>
    <div style='max-width:1100px;margin:0 auto;padding:24px;'>
        <div style='background:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 8px 24px rgba(0,0,0,0.08);'>
            <div style='background:linear-gradient(135deg,#111827,#1f2937);padding:24px 28px;color:#ffffff;'>
                <div style='font-size:16px;opacity:0.90;'>📊 Sistema de Seguimiento</div>
                <div style='font-size:38px;font-weight:800;line-height:1.1;margin-top:6px;'>Informe Diario de Embarques</div>
                <div style='font-size:14px;opacity:0.90;margin-top:10px;'>
                    Generado: {DateTime.Now:dd/MM/yyyy HH:mm}
                </div>
            </div>

            <div style='padding:24px 28px 10px 28px;'>
                <table style='width:100%;border-collapse:collapse;margin-bottom:18px;'>
                    <tr>
                        <td style='padding:8px 0;font-size:14px;'><strong>Rango:</strong> {rango}</td>
                        <td style='padding:8px 0;font-size:14px;'><strong>Marca:</strong> {marcaTexto}</td>
                    </tr>
                </table>

                <table style='width:100%;border-collapse:separate;border-spacing:12px 0;margin-bottom:24px;'>
                    <tr>
                        <td style='background:#2563eb;color:#fff;border-radius:16px;padding:20px;text-align:center;width:16%;'>
                            <div style='font-size:16px;font-weight:600;'>Total</div>
                            <div style='font-size:34px;font-weight:800;margin-top:8px;'>{total}</div>
                        </td>
                        <td style='background:#16a34a;color:#fff;border-radius:16px;padding:20px;text-align:center;width:16%;'>
                            <div style='font-size:16px;font-weight:600;'>OTIF</div>
                            <div style='font-size:34px;font-weight:800;margin-top:8px;'>{otif}%</div>
                        </td>
                        <td style='background:#f59e0b;color:#fff;border-radius:16px;padding:20px;text-align:center;width:16%;'>
                            <div style='font-size:16px;font-weight:600;'>En tránsito</div>
                            <div style='font-size:34px;font-weight:800;margin-top:8px;'>{enTransito}</div>
                        </td>
                        <td style='background:#dc2626;color:#fff;border-radius:16px;padding:20px;text-align:center;width:16%;'>
                            <div style='font-size:16px;font-weight:600;'>Retrasados</div>
                            <div style='font-size:34px;font-weight:800;margin-top:8px;'>{retrasados}</div>
                        </td>
                        <td style='background:#4b5563;color:#fff;border-radius:16px;padding:20px;text-align:center;width:16%;'>
                            <div style='font-size:16px;font-weight:600;'>Entregados</div>
                            <div style='font-size:34px;font-weight:800;margin-top:8px;'>{entregados}</div>
                        </td>
                        <td style='background:#6b7280;color:#fff;border-radius:16px;padding:20px;text-align:center;width:16%;'>
                            <div style='font-size:16px;font-weight:600;'>Cerrados</div>
                            <div style='font-size:34px;font-weight:800;margin-top:8px;'>{cerrados}</div>
                        </td>
                    </tr>
                </table>
");

            if (retrasados > 0)
            {
                sb.Append($@"
                <div style='background:#fef2f2;border:1px solid #fecaca;color:#991b1b;border-radius:12px;padding:14px 16px;margin-bottom:24px;font-size:14px;'>
                    🚨 Hay <strong>{retrasados}</strong> embarques retrasados.
                </div>");
            }
            else
            {
                sb.Append(@"
                <div style='background:#ecfdf5;border:1px solid #bbf7d0;color:#166534;border-radius:12px;padding:14px 16px;margin-bottom:24px;font-size:14px;'>
                    ✅ No existen embarques retrasados en el filtro aplicado.
                </div>");
            }

            if (ranking != null && ranking.Count > 0)
            {
                sb.Append(@"
                <div style='margin-bottom:28px;'>
                    <div style='font-size:20px;font-weight:800;margin-bottom:12px;color:#111827;'>🏆 Ranking de Marcas</div>
                    <table style='width:100%;border-collapse:collapse;font-size:14px;'>
                        <thead>
                            <tr style='background:#111827;color:#ffffff;'>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>#</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Marca</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Total</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Retrasos</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>OTIF</th>
                            </tr>
                        </thead>
                        <tbody>");

                int pos = 1;
                foreach (var item in ranking)
                {
                    string colorOtif = item.OTIF >= 90 ? "#166534" :
                                       item.OTIF >= 80 ? "#92400e" : "#991b1b";

                    sb.Append($@"
                            <tr>
                                <td style='padding:10px;border:1px solid #e5e7eb;text-align:center;'>{pos}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;'><strong>{item.Marca}</strong></td>
                                <td style='padding:10px;border:1px solid #e5e7eb;text-align:center;'>{item.Total}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;text-align:center;'>{item.Retrasos}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;text-align:center;color:{colorOtif};font-weight:700;'>
                                    {Math.Round(item.OTIF, 0)}%
                                </td>
                            </tr>");
                    pos++;
                }

                sb.Append(@"
                        </tbody>
                    </table>
                </div>");
            }

            if (resumenPorMarca != null && resumenPorMarca.Count > 0)
            {
                sb.Append(@"
                <div style='margin-bottom:24px;'>
                    <div style='font-size:20px;font-weight:800;margin-bottom:12px;color:#111827;'>📦 Resumen por Marca / ETA</div>
                    <table style='width:100%;border-collapse:collapse;font-size:13px;'>
                        <thead>
                            <tr style='background:#111827;color:#ffffff;'>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Marca</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Shipment</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>ETA</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Departure</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Destino</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Naviera</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Cantidad</th>
                                <th style='padding:10px;border:1px solid #e5e7eb;'>Estado</th>
                            </tr>
                        </thead>
                        <tbody>");

                foreach (var item in resumenPorMarca)
                {
                    string fondoEstado = item.Estado == "Entregado" ? "#dcfce7" :
                                         item.Estado == "Retrasado" ? "#fee2e2" : "#fef3c7";

                    string colorEstado = item.Estado == "Entregado" ? "#166534" :
                                         item.Estado == "Retrasado" ? "#991b1b" : "#92400e";

                    sb.Append($@"
                            <tr>
                                <td style='padding:10px;border:1px solid #e5e7eb;'><strong>{item.Marca}</strong></td>
                                <td style='padding:10px;border:1px solid #e5e7eb;'>{item.Shipment}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;text-align:center;'>{(item.ETA != null ? item.ETA.Value.ToString("dd/MM/yyyy") : "-")}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;text-align:center;'>{(item.Departure != null ? item.Departure.Value.ToString("dd/MM/yyyy") : "-")}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;'>{item.Destino}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;'>{item.Naviera}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;text-align:center;'>{item.Cantidad}</td>
                                <td style='padding:10px;border:1px solid #e5e7eb;text-align:center;'>
                                    <span style='display:inline-block;padding:6px 12px;border-radius:999px;background:{fondoEstado};color:{colorEstado};font-weight:700;'>
                                        {item.Estado}
                                    </span>
                                </td>
                            </tr>");
                }

                sb.Append(@"
                        </tbody>
                    </table>
                </div>");
            }

            sb.Append(@"
                <div style='margin-top:16px;font-size:12px;color:#6b7280;'>
                    Generado automáticamente por el sistema de seguimiento LozanoAsesores.
                </div>
            </div>
        </div>
    </div>
</body>
</html>");

            return sb.ToString();
        }
    }
}