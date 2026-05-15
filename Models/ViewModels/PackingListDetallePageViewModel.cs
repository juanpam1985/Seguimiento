using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Seguimiento.Models;

namespace Seguimiento.Models.ViewModels
{
    public class PackingListDetallePageViewModel
    {
        [ValidateNever]
        public PackingList? Packing { get; set; }

        public PackingListDetalle NuevoDetalle { get; set; } = new PackingListDetalle();

        [ValidateNever]
        public List<Modelo> Modelos { get; set; } = new List<Modelo>();

        [ValidateNever]
        public List<Color> Colores { get; set; } = new List<Color>();

        public int SubtramiteId { get; set; }

        [ValidateNever]
        public string? SubtramiteTitulo { get; set; }
    }
}