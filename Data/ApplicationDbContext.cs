using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Seguimiento.Models;

namespace Seguimiento.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
        {
        }

        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }

        public DbSet<Marca> Marcas { get; set; }
        public DbSet<Modelo> Modelos { get; set; }

        public DbSet<Pregunta> Preguntas { get; set; }
        public DbSet<OpcionLista> OpcionesLista { get; set; }

        public DbSet<Embarque> Embarques { get; set; }
        public DbSet<Respuesta> Respuestas { get; set; }
        public DbSet<ModeloVehiculo> ModelosVehiculos { get; set; }

        public DbSet<EmbarqueModelo> EmbarqueModelos { get; set; }
        public DbSet<UsuarioEmpresa> UsuarioEmpresas { get; set; }



    }
}