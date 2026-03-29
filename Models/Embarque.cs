using System;

namespace Seguimiento.Models
{
    public class Embarque
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }

        public DateTime FechaRegistro { get; set; }

        // =========================
        // 🔥 AUDITORÍA (NUEVO)
        // =========================
        public string? CreadoPor { get; set; }
        public DateTime? FechaCreacion { get; set; }

        public string? ModificadoPor { get; set; }
        public DateTime? FechaModificacion { get; set; }

        // =========================
        // RELACIONES (si ya las tienes, déjalas)
        // =========================
        public Empresa? Empresa { get; set; }

        public ICollection<EmbarqueModelo>? EmbarqueModelos { get; set; }
        public ICollection<Respuesta>? Respuestas { get; set; }
        public bool? DepartureConfirmado { get; set; } = false;
        public bool? EtaConfirmado { get; set; } = false;
    }
}