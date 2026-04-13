using System;
using System.Collections.Generic;

namespace Seguimiento.Models
{
    public class Embarque
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }

        public DateTime FechaRegistro { get; set; }

        // =========================
        // AUDITORÍA
        // =========================
        public string? CreadoPor { get; set; }
        public DateTime? FechaCreacion { get; set; }

        public string? ModificadoPor { get; set; }
        public DateTime? FechaModificacion { get; set; }

        // =========================
        // CONFIRMACIONES
        // =========================
        public bool? DepartureConfirmado { get; set; } = false;
        public bool? EtaConfirmado { get; set; } = false;

        // =========================
        // SUBTRÁMITES
        // =========================
        public bool EsSubtramite { get; set; } = false;
        public int? EmbarquePadreId { get; set; }
        public int? OrdenSubtramite { get; set; }

        // =========================
        // RELACIONES
        // =========================
        public Empresa? Empresa { get; set; }

        public Embarque? EmbarquePadre { get; set; }
        public ICollection<Embarque>? Subtramites { get; set; } = new List<Embarque>();

        public ICollection<EmbarqueModelo>? EmbarqueModelos { get; set; }
        public ICollection<Respuesta>? Respuestas { get; set; }
    }
}