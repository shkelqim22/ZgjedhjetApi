using Microsoft.EntityFrameworkCore;
using ZgjedhjetApi.Models.Entities;

namespace ZgjedhjetApi.Data
{
    public class LifeDbContext : DbContext
    {
        public DbSet<Zgjedhjet> Zgjedhjet { get; set; } = null!;

        public LifeDbContext(DbContextOptions<LifeDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure table/column names to match CSV exact names if needed.
            modelBuilder.Entity<Zgjedhjet>(eb =>
            {
                eb.ToTable("Zgjedhjet");
                eb.Property(e => e.Qendra_e_Votimit).HasColumnName("Qendra_e_Votimit");
                eb.Property(e => e.VendVotimi).HasColumnName("Vendvotimi");
                eb.Property(e => e.Vota).HasColumnName("Vota");
            });
        }
    }
}
