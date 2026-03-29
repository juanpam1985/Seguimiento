namespace Seguimiento.Models
{
    public class MarcaEmpresa
    {
        public int MarcaId { get; set; }
        public Marca Marca { get; set; }

        public int EmpresaId { get; set; }
        public Empresa Empresa { get; set; }
    }
}