using Microsoft.EntityFrameworkCore;
using InkMath.AuthService.Models;

namespace InkMath.AuthService.Data
{
    public class AplicationDbContext : DbContext
    {
        public AplicationDbContext(DbContextOptions<AplicationDbContext> options) : base(options) { }

        // Catálogos
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Plan> Planes { get; set; }
        public DbSet<EstadoSuscripcion> EstadosSuscripcion { get; set; }
        public DbSet<TipoRecurso> TiposRecurso { get; set; }
        public DbSet<TipoPregunta> TiposPregunta { get; set; }

        // Entidades Principales
        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Aula> Aulas { get; set; } = null!;
        public DbSet<Suscripcion> Suscripciones { get; set; } = null!;
        public DbSet<Recurso> Recursos { get; set; }
        public DbSet<TestPersonalizado> TestsPersonalizados { get; set; }

        // Referencia a las tablas de transacciones
        public DbSet<SaldoMoneda> SaldoMonedas { get; set; } = null!;
        public DbSet<TransaccionMoneda> TransaccionesMonedas { get; set; } = null!;
        public DbSet<AlertaRezago> AlertasRezago { get; set; } = null!;

        // Relaciones M:N
        public DbSet<AulaEstudiante> AulaEstudiantes { get; set; }
        public DbSet<AulaRecurso> AulasRecursos { get; set; }

        // Detalle Compuesto        
        public DbSet<PreguntaTest> PreguntasTest { get; set; }
        public DbSet<OpcionPregunta> OpcionesPregunta { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Filtros globales de Soft Delete
            modelBuilder.Entity<TestPersonalizado>().HasQueryFilter(t => t.EstaActivo);
            modelBuilder.Entity<Aula>().HasQueryFilter(a => a.EstaActivo);
            //modelBuilder.Entity<Usuario>().HasQueryFilter(u => u.EstaActivo);
            //modelBuilder.Entity<Recurso>().HasQueryFilter(r => r.EstaActivo);
            //modelBuilder.Entity<Suscripcion>().HasQueryFilter(s => s.EstaActivo);

            // Llave primaria compuesta requerida para la tabla particionada transacciones_monedas
            modelBuilder.Entity<TransaccionMoneda>()
                .HasKey(t => new { t.Id, t.CreadoEn });

            modelBuilder.Entity<TransaccionMoneda>()
                .Property(t => t.Id)
                .ValueGeneratedOnAdd();

            // Clave primaria compuesta para aula_estudiante
            modelBuilder.Entity<AulaEstudiante>()
                .HasKey(ae => new { ae.AulaId, ae.EstudianteId });

            // Clave primaria compuesta para aula_recurso
            modelBuilder.Entity<AulaRecurso>()
                .HasKey(ar => new { ar.AulaId, ar.RecursoId });

            // Configuración de Relaciones para aula_recurso
            modelBuilder.Entity<AulaRecurso>()
                .HasOne(ar => ar.Aula)
                .WithMany()
                .HasForeignKey(ar => ar.AulaId);

            modelBuilder.Entity<AulaRecurso>()
                .HasOne(ar => ar.Recurso)
                .WithMany()
                .HasForeignKey(ar => ar.RecursoId);
        }
    }
}
