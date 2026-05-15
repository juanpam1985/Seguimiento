using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;
using Seguimiento.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

    public async Task<IActionResult> Index(DateTime? fechaInicio, DateTime? fechaFin, string marcaFiltro = "")
    {
        if (!Puede("VER"))
            return RedirectToAction("Dashboard", "Embarques");

        var empresaId = HttpContext.Session.GetInt32("Id");

        if (empresaId == null)
            return RedirectToAction("Login", "Account");

        var query = _context.Embarques
            .Where(e => e.EmpresaId == empresaId && !e.EsSubtramite)
            .Include(e => e.Respuestas)
                .ThenInclude(r => r.Pregunta)
            .AsQueryable();

        if (fechaInicio.HasValue)
            query = query.Where(e => e.FechaRegistro >= fechaInicio.Value.Date);

        if (fechaFin.HasValue)
            query = query.Where(e => e.FechaRegistro <= fechaFin.Value.Date.AddDays(1).AddTicks(-1));

        var lista = await query.ToListAsync();

        // SOLO marcas asignadas a la empresa
        var marcasEmpresa = await _context.Marcas
            .AsNoTracking()
            .Where(m => m.Activo && m.MarcaEmpresas.Any(me => me.EmpresaId == empresaId.Value))
            .OrderBy(m => m.Nombre)
            .ToListAsync();

        ViewBag.MarcasCombo = marcasEmpresa;

        int total = 0;
        int retrasados = 0;
        int enTransito = 0;
        int entregados = 0;
        int abiertos = 0;
        int cerrados = 0;

        var marcas = new Dictionary<string, (int Total, int Retrasos)>();

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
                marca = marcasEmpresa.FirstOrDefault(m => m.Id == marcaId)?.Nombre ?? "SIN MARCA";
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
            marcas[e.Marca] = (marcaActual.Total + 1, marcaActual.Retrasos);

            if (!EsCerrado(e.Status))
                abiertos++;

            if (EsCerrado(e.Status))
                cerrados++;

            if (EsCerrado(e.Status))
                entregados++;

            if (e.EsRetrasado)
            {
                retrasados++;

                var actual = marcas[e.Marca];
                marcas[e.Marca] = (actual.Total, actual.Retrasos + 1);
            }

            if (!EsCerrado(e.Status))
                enTransito++;
        }

        double otif = total > 0
            ? ((double)(total - retrasados) / total) * 100
            : 0;

        var ranking = marcas
            .Where(m => m.Key != "SIN MARCA")
            .Select(m => new
            {
                Marca = m.Key,
                Total = m.Value.Total,
                Retrasos = m.Value.Retrasos,
                OTIF = m.Value.Total > 0
                    ? ((double)(m.Value.Total - m.Value.Retrasos) / m.Value.Total) * 100
                    : 0
            })
            .OrderByDescending(x => x.OTIF)
            .ToList();

        var resumenPorMarca = detalle
            .OrderBy(x => x.ETA ?? DateTime.MaxValue)
            .ThenBy(x => x.Marca)
            .Select(x => new
            {
                x.Marca,
                x.Shipment,
                ETA = x.ETA,
                Departure = x.Departure,
                x.Destino,
                x.Naviera,
                x.Cantidad,
                Estado = EsCerrado(x.Status)
                    ? "Entregado"
                    : x.EsRetrasado
                        ? "Retrasado"
                        : "En tránsito"
            })
            .Cast<object>()
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
        ViewBag.OTIF = (int)Math.Round(otif, 0);

        ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
        ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");
        ViewBag.MarcaFiltro = marcaFiltro;
        ViewBag.ResumenPorMarca = resumenPorMarca;

        return View();
    }
}