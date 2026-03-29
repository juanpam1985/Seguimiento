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
        public DbSet<TipoModelo> TipoModelo { get; set; }
        public DbSet<OpcionLista> OpcionesLista { get; set; }

        public DbSet<Embarque> Embarques { get; set; }
        public DbSet<Respuesta> Respuestas { get; set; }
        public DbSet<UsuarioEmpresa> UsuarioEmpresas { get; set; }
        public DbSet<EmbarqueModelo> EmbarqueModelos { get; set; }

        public DbSet<AuditoriaEmbarque> AuditoriaEmbarques { get; set; }
        // RELACIONES
        public DbSet<MarcaEmpresa> MarcaEmpresas { get; set; }
        public DbSet<Rol> Roles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MarcaEmpresa>()
                .HasKey(me => new { me.MarcaId, me.EmpresaId });

            modelBuilder.Entity<MarcaEmpresa>()
                .HasOne(me => me.Marca)
                .WithMany(m => m.MarcaEmpresas)
                .HasForeignKey(me => me.MarcaId);

            modelBuilder.Entity<MarcaEmpresa>()
                .HasOne(me => me.Empresa)
                .WithMany(e => e.MarcaEmpresas)
                .HasForeignKey(me => me.EmpresaId);
        }
    }
}