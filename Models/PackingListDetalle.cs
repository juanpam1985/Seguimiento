using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Seguimiento.Models
{
    public class PackingListDetalle
    {
        public int Id { get; set; }

        public int PackingListId { get; set; }

        [Display(Name = "Línea")]
        public int Linea { get; set; }

        [StringLength(150)]
        [Display(Name = "Origin Port")]
        public string? OriginPort { get; set; }

        [StringLength(150)]
        [Display(Name = "Destination Port")]
        public string? DestinationPort { get; set; }

        [StringLength(150)]
        public string? Ship { get; set; }

        [StringLength(100)]
        [Display(Name = "Invoice Number")]
        public string? InvoiceNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "B/L")]
        public string? BL { get; set; }

        [StringLength(100)]
        [Display(Name = "VIN No.")]
        public string? VinNo { get; set; }

        [Display(Name = "Modelo")]
        public int? ModeloId { get; set; }

        [StringLength(150)]
        [Display(Name = "Model")]
        public string? ModelPacking { get; set; }

        [StringLength(150)]
        [Display(Name = "Model (SRI)")]
        public string? ModelSRI { get; set; }

        [StringLength(150)]
        [Display(Name = "Model (HOMOLOGACIÓN)")]
        public string? ModelHomologacion { get; set; }

        [StringLength(100)]
        [Display(Name = "Engine Code")]
        public string? EngineCode { get; set; }

        [StringLength(100)]
        [Display(Name = "Engine Number")]
        public string? EngineNumber { get; set; }

        [Display(Name = "Color")]
        public int? ColorId { get; set; }

        [StringLength(50)]
        [Display(Name = "Color")]
        public string? ColorCodigo { get; set; }

        [StringLength(150)]
        [Display(Name = "Color Name")]
        public string? ColorName { get; set; }

        [StringLength(150)]
        [Display(Name = "Color Local")]
        public string? ColorLocal { get; set; }

        [StringLength(50)]
        [Display(Name = "Model Year")]
        public string? ModelYear { get; set; }

        [Display(Name = "Net Weight (KGs)")]
        public decimal? NetWeightKg { get; set; }

        public decimal? CBM { get; set; }

        public decimal? Precio { get; set; }

        public decimal? Flete { get; set; }

        public decimal? Seguro { get; set; }

        [StringLength(100)]
        public string? Incoterm { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [ValidateNever]
        public PackingList? PackingList { get; set; }

        [ValidateNever]
        public Modelo? Modelo { get; set; }

        [ValidateNever]
        public Color? Color { get; set; }
    }
}