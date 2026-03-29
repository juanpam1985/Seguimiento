using Seguimiento.Models;

public class TipoModelo
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public bool Activo { get; set; }

    public List<Modelo> Modelos { get; set; }
}