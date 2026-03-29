using System;

namespace Seguimiento.Models
{
    public class Respuesta
    {
        public int Id { get; set; }

        public int EmbarqueId { get; set; }

        public int PreguntaId { get; set; }

        public string Valor { get; set; } // 🔥 NUEVO CAMPO

        public DateTime? FechaRegistro { get; set; }
        public Pregunta? Pregunta { get; set; }
    }
}