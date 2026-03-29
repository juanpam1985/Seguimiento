using System.ComponentModel.DataAnnotations;

namespace Seguimiento.Models
{
    public class Empresa
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; }

        public bool Activo { get; set; }

        public ICollection<UsuarioEmpresa> UsuarioEmpresas { get; set; } = new List<UsuarioEmpresa>();

        public ICollection<MarcaEmpresa> MarcaEmpresas { get; set; } = new List<MarcaEmpresa>();
    }
}