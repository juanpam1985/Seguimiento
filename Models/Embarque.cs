using System;
using System.ComponentModel.DataAnnotations;

namespace Seguimiento.Models
{
    public class Embarque
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }

        public string Status { get; set; }

        public string Brand { get; set; }

        public string Shipment { get; set; }

        public string DestinationPort { get; set; }

        public string Incoterm { get; set; }

        public string ShippingCompany { get; set; }

        public int Qty { get; set; }

        public DateTime? DepartureDate { get; set; }

        public DateTime? ETA { get; set; }

        public string Estado { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
