namespace Seguimiento.Models
{
    public class EmbarqueModelo
    {
        public int Id { get; set; }

        public int EmbarqueId { get; set; }

        public int ModeloId { get; set; }
        // 🔥 ESTA LÍNEA ES LA CLAVE
        public Modelo Modelo { get; set; }
        public int Cantidad { get; set; }
      


    }
}