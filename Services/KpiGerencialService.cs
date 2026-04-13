using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;
using Seguimiento.Models;

namespace Seguimiento.Services
{
    public class KpiGerencialService
    {
        private readonly ApplicationDbContext _context;

        public KpiGerencialService(ApplicationDbContext context)
        {
            _context = context;
        }

        public class DetalleKPI
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

        public class RankingMarcaDto
        {
            public string Marca { get; set; } = "";
            public int Total { get; set; }
            public int Retrasos { get; set; }
            public double OTIF { get; set; }
        }

        public class ResumenMarcaDto
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

        public class ResultadoKpiGerencial
        {
            public int Total { get; set; }
            public int Retrasados { get; set; }
            public int EnTransito { get; set; }
            public int Entregados { get; set; }
            public int Abiertos { get; set; }
            public int Cerrados { get; set; }
            public int OTIF { get; set; }

            public List<string> Marcas { get; set; } = new();
            public List<int> MarcasValores { get; set; } = new();
            public List<RankingMarcaDto> Ranking { get; set; } = new();
            public List<ResumenMarcaDto> ResumenPorMarca { get; set; } = new();
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

        public async Task<ResultadoKpiGerencial> CalcularAsync(
            int empresaId,
            DateTime? fechaInicio = null,
            DateTime? fechaFin = null,
            string marcaFiltro = "")
        {
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
                        !string.IsNullOrWhiteSpace(r.Pregunta.Nombre) &&
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

            int total = detalle.Count;
            int retrasados = 0;
            int enTransito = 0;
            int entregados = 0;
            int abiertos = 0;
            int cerrados = 0;

            var marcas = new Dictionary<string, (int total, int retrasos)>();

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
                .Select(m => new RankingMarcaDto
                {
                    Marca = m.Key,
                    Total = m.Value.total,
                    Retrasos = m.Value.retrasos,
                    OTIF = m.Value.total > 0
                        ? ((double)(m.Value.total - m.Value.retrasos) / m.Value.total) * 100
                        : 0
                })
                .OrderByDescending(x => x.OTIF)
                .ThenBy(x => x.Marca)
                .ToList();

            var resumenPorMarca = detalle
                .OrderBy(x => x.ETA ?? DateTime.MaxValue)
                .ThenBy(x => x.Marca)
                .Select(x => new ResumenMarcaDto
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

            return new ResultadoKpiGerencial
            {
                Total = total,
                Retrasados = retrasados,
                EnTransito = enTransito,
                Entregados = entregados,
                Abiertos = abiertos,
                Cerrados = cerrados,
                OTIF = (int)Math.Round(otif, 0),
                Marcas = ranking.Select(x => x.Marca).ToList(),
                MarcasValores = ranking.Select(x => x.Retrasos).ToList(),
                Ranking = ranking,
                ResumenPorMarca = resumenPorMarca
            };
        }
    }
}