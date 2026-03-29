using System;
using System.Collections.Generic;

namespace Seguimiento.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        public string UsuarioLogin { get; set; }

        public string Nombre { get; set; }

        public string Email { get; set; }

        public string PasswordHash { get; set; }

        public bool Activo { get; set; }

        public DateTime FechaCreacion { get; set; }

        public ICollection<UsuarioEmpresa> UsuarioEmpresas { get; set; }
        public int RolId { get; set; }   // ya lo tienes en BD

        public Rol Rol { get; set; }     // 🔥 ESTA ES LA CLAVE

    }
}
