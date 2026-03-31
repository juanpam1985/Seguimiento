namespace Seguimiento.Models
{
    public class RolPermiso
    {
        public int Id { get; set; }

        public int RolId { get; set; }
        public Rol Rol { get; set; }

        public int ModuloId { get; set; }
        public Modulo Modulo { get; set; }

        public bool PuedeVer { get; set; }
        public bool PuedeCrear { get; set; }
        public bool PuedeEditar { get; set; }
        public bool PuedeEliminar { get; set; }
    }
}