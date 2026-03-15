using System.ComponentModel.DataAnnotations;

namespace Seguimiento.Models
{
    public class OpcionLista
    {
        public int Id { get; set; }

        public int? PreguntaId { get; set; }

        public string Valor { get; set; }

        public int? Orden { get; set; }

        public bool Activo { get; set; } = true;

        // Relación con Pregunta
        public Pregunta Pregunta { get; set; }
    }
}
