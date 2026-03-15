namespace Seguimiento.Models
{
    public class Empresa
    {
        public int Id { get; set; }

        public string Nombre { get; set; }

        public bool Activo { get; set; }
        public ICollection<UsuarioEmpresa> UsuarioEmpresas { get; set; }

    }
}