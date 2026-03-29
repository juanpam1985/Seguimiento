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

        public bool Activo { get; set; }

        // 🔥 navegación sin validación
        [ValidateNever]
        public Marca Marca { get; set; }

        // 🔥 AQUÍ ESTÁ LA CLAVE
        [Required(ErrorMessage = "Debe seleccionar un tipo de modelo")]
        public int TipoModeloId { get; set; } // ❗ QUITAMOS EL ?

        [ValidateNever]
        public TipoModelo TipoModelo { get; set; }
    }
}