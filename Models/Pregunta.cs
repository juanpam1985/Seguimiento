namespace Seguimiento.Models
{
    public class Pregunta
    {
        public int Id { get; set; }

        public int? ModeloId { get; set; }

        public string Nombre { get; set; }


  


        public int Orden { get; set; }

        public bool Activo { get; set; }

        public bool EsGeneral { get; set; }

        // Relaciones
        public Modelo? Modelo { get; set; }

 

        public List<Respuesta>? Respuestas { get; set; }
    }
}