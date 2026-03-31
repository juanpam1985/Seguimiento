namespace Seguimiento.Models
{
    public class Rol
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public bool Activo { get; set; }

        public ICollection<Usuario> Usuarios { get; set; }
        public ICollection<RolPermiso> RolPermisos { get; set; }
    }
}