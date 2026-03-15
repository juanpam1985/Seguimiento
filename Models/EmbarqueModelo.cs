using System.ComponentModel.DataAnnotations;

namespace Seguimiento.Models
{
    public class EmbarqueModelo
    {
        public int Id { get; set; }

        public int EmbarqueId { get; set; }

        public int ModeloVehiculoId { get; set; }

        public int Cantidad { get; set; }
    }
}
