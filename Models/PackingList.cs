using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Seguimiento.Models
{
    public class PackingList
    {
        public int Id { get; set; }

        [Display(Name = "Subtrámite")]
        public int SubtramiteId { get; set; }

        [StringLength(100)]
        [Display(Name = "Request No.")]
        public string? RequestNo { get; set; }

        [Display(Name = "Fecha Embarque")]
        [DataType(DataType.Date)]
        public DateTime? FechaEmbarque { get; set; }

        [Display(Name = "Fecha Arribo")]
        [DataType(DataType.Date)]
        public DateTime? FechaArribo { get; set; }

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        [Display(Name = "Fecha Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [ValidateNever]
        public Embarque? Subtramite { get; set; }

        [ValidateNever]
        public List<PackingListDetalle> Detalles { get; set; } = new List<PackingListDetalle>();
    }
}