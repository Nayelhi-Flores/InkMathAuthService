using Microsoft.EntityFrameworkCore;
using InkMath.AuthService.Models;

namespace InkMath.AuthService.Data
{
    public class AplicationDbContext : DbContext
    {
        public AplicationDbContext(DbContextOptions<AplicationDbContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; } = null!;

        // Referencia a las tablas de transacciones
        public DbSet<SaldoMoneda> SaldoMonedas { get; set; } = null!;
        public DbSet<TransaccionMoneda> TransaccionesMonedas { get; set; } = null!;
        public DbSet<Suscripcion> Suscripciones { get; set; } = null!;
        public DbSet<Aula> Aulas { get; set; } = null!;
        public DbSet<AlertaRezago> AlertasRezago { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Llave primaria compuesta requerida para la tabla particionada transacciones_monedas
            modelBuilder.Entity<TransaccionMoneda>()
                .HasKey(t => new { t.Id, t.CreadoEn });

            modelBuilder.Entity<TransaccionMoneda>()
                .Property(t => t.Id)
                .ValueGeneratedOnAdd();
        }
    }
}
