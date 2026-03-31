namespace Seguimiento.Models
{
    public class Modulo
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Clave { get; set; }   // Ej: USUARIOS, EMPRESAS, EMBARQUES
        public bool Activo { get; set; } = true;

        public ICollection<RolPermiso> RolPermisos { get; set; }
    }
}