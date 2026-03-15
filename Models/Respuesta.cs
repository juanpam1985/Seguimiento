using System;

namespace Seguimiento.Models
{
    public class Respuesta
    {
        public int Id { get; set; }

        public int EmbarqueId { get; set; }

        public int PreguntaId { get; set; }

        public string ValorTexto { get; set; }

        public decimal? ValorNumero { get; set; }

        public DateTime? ValorFecha { get; set; }

        public int? ValorListaId { get; set; }

        public DateTime FechaRegistro { get; set; }
    }
}