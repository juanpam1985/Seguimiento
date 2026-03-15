namespace Seguimiento.Models
{
    public class Modelo
    {
        public int Id { get; set; }

        public int MarcaId { get; set; }

        public string Nombre { get; set; }

        public bool Activo { get; set; }

        public Marca Marca { get; set; }
    }
}