namespace Seguimiento.Models
{
    public class Marca
    {
        public int Id { get; set; }

        public string Nombre { get; set; }

        public bool Activo { get; set; }

        public ICollection<MarcaEmpresa> MarcaEmpresas { get; set; } = new List<MarcaEmpresa>();

        public ICollection<Modelo> Modelos { get; set; } = new List<Modelo>();
    }
}