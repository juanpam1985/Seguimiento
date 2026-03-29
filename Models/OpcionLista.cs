using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Seguimiento.Models
{
    public class OpcionLista
    {
        [Key]
        public int Id { get; set; }

        public int? PreguntaId { get; set; }

        [Required] // 🔥 importante
        public string Valor { get; set; }

        public int? Orden { get; set; }

        public bool Activo { get; set; } = true;

        // 🔥 CLAVE: ignorar validación MVC
        [ValidateNever]
        public Pregunta Pregunta { get; set; }
    }
}