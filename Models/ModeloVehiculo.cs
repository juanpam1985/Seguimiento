using System.ComponentModel.DataAnnotations;

namespace Seguimiento.Models
{
    public class ModeloVehiculo
    {
        public int Id { get; set; }

        public int EmpresaId { get; set; }

        [Required]
        public string Nombre { get; set; }

        public int Orden { get; set; }

        public bool Activo { get; set; } = true;
        public Marca Marca { get; set; }
    }
}
