using Microsoft.EntityFrameworkCore;
using InkMath.AuthService.Models;

namespace InkMath.AuthService.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
    }
}
