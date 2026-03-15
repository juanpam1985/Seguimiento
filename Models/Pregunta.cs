namespace Seguimiento.Models
{
    public class Pregunta
    {
        public int Id { get; set; }

        public string Nombre { get; set; }

        public int TipoCampoId { get; set; }

        public int Orden { get; set; }

        public bool Activo { get; set; }
    }
}