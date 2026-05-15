using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Seguimiento.Models
{
    public class Modelo
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una marca")]
        public int MarcaId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; }

        [StringLength(150)]
        [Display(Name = "Model")]
        public string ModelPacking { get; set; }

        [StringLength(150)]
        [Display(Name = "Model (SRI)")]
        public string ModelSRI { get; set; }

        [StringLength(150)]
        [Display(Name = "Model (HOMOLOGACIÓN)")]
        public string ModelHomologacion { get; set; }

        public bool Activo { get; set; }

        [ValidateNever]
        public Marca Marca { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un tipo de modelo")]
        public int TipoModeloId { get; set; }

        [ValidateNever]
        public TipoModelo TipoModelo { get; set; }
    }
}