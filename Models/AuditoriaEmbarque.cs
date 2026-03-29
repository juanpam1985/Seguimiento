using System;

namespace Seguimiento.Models
{
    public class AuditoriaEmbarque
    {
        public int Id { get; set; }

        public int EmbarqueId { get; set; }

        public string? Usuario { get; set; }
        public string? Accion { get; set; }
        public DateTime Fecha { get; set; }
        public string? Detalle { get; set; }
    }
}