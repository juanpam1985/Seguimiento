using System.ComponentModel.DataAnnotations.Schema;

namespace Seguimiento.Models
{
    [Table("UsuarioEmpresa")]
    public class UsuarioEmpresa
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; }

        public int EmpresaId { get; set; } // 🔥 ESTA LÍNEA FALTABA
        public Empresa Empresa { get; set; }
    }
}