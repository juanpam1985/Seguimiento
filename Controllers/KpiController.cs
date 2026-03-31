using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;
using Seguimiento.Helpers;

public class KPIController : Controller
{
    private readonly ApplicationDbContext _context;

    public KPIController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int? RolIdSesion()
    {
        return HttpContext.Session.GetInt32("RolId");
    }

    private bool Puede(string accion)
    {
        return PermisoHelper.TienePermiso(_context, RolIdSesion(), "KPI", accion);
    }

    public async Task<IActionResult> Index(DateTime? fechaInicio, DateTime? fechaFin)
    {
        if (!Puede("VER"))
            return RedirectToAction("Dashboard", "Embarques");

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
            .ToListAsync();

        int total = lista.Count;
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

        foreach (var e in lista)
        {
            var respuestas = e.Respuestas ?? new List<Respuesta>();

            string GetValor(string nombre)
            {
                return respuestas.FirstOrDefault(r =>
                    r.Pregunta.Nombre.ToUpper().Contains(nombre.ToUpper())
                )?.Valor ?? "";
            }

            DateTime? GetFecha(string nombre)
            {
                var val = GetValor(nombre);
                return DateTime.TryParse(val, out DateTime d) ? d : (DateTime?)null;
            }

            var departure = GetFecha("DEPARTURE");
            var status = GetValor("STATUS").ToLower();

            if (status == "open") abiertos++;
            if (status == "closed") cerrados++;

            bool esRetrasado = false;

            if (departure.HasValue && departure.Value.Date.AddDays(1) < DateTime.Now.Date)
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

            if (!marcas.ContainsKey(marca))
                marcas[marca] = (0, 0);

            marcas[marca] = (marcas[marca].total + 1, marcas[marca].retrasos);

            if (status == "closed")
            {
                entregados++;
            }
            else if (esRetrasado)
            {
                retrasados++;

                var actual = marcas[marca];
                marcas[marca] = (actual.total, actual.retrasos + 1);
            }
            else
            {
                enTransito++;
            }
        }

        double otif = total > 0 ? ((double)(total - retrasados) / total) * 100 : 0;

        var ranking = marcas
            .Select(m => new
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

        ViewBag.Marcas = ranking.Select(x => x.Marca).ToList();
        ViewBag.MarcasValores = ranking.Select(x => x.Retrasos).ToList();
        ViewBag.Ranking = ranking;

        ViewBag.Total = total;
        ViewBag.Retrasados = retrasados;
        ViewBag.EnTransito = enTransito;
        ViewBag.Entregados = entregados;
        ViewBag.Abiertos = abiertos;
        ViewBag.Cerrados = cerrados;
        ViewBag.OTIF = Math.Round(otif, 1);

        ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
        ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");

        return View();
    }
}