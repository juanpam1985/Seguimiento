using System;
using System.ComponentModel.DataAnnotations;

namespace Seguimiento.Models
{
    public class Color
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El código es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Código")]
        public string Codigo { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150)]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [StringLength(150)]
        [Display(Name = "Nombre Local")]
        public string NombreLocal { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}