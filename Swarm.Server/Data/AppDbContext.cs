using Microsoft.EntityFrameworkCore;
using Swarm.Server.Entities;

namespace Swarm.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<JobCompletion> JobCompletions => Set<JobCompletion>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // ---- Clients ----
            b.Entity<Client>(e =>
            {
                e.ToTable("Clients");
                e.HasKey(x => x.Id);

                e.Property(x => x.IpOrHost)
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.Port)
                    .IsRequired();

                e.Property(x => x.DisplayName)
                    .HasMaxLength(200);

                e.Property(x => x.LastSeenUtc)
                    .IsRequired();

                e.Property(x => x.TotalJobsDone)
                    .IsRequired()
                    .HasDefaultValue(0);

                // Unique client per (IpOrHost, Port)
                e.HasIndex(x => new { x.IpOrHost, x.Port })
                 .IsUnique();
            });

            // ---- JobCompletions ----
            b.Entity<JobCompletion>(e =>
            {
                e.ToTable("JobCompletions");
                e.HasKey(x => x.Id);

                e.Property(x => x.PythonB64).IsRequired();
                e.Property(x => x.Sha256Hex).IsRequired().HasMaxLength(64);
                e.Property(x => x.ResultB64);
                e.Property(x => x.FinishedUtc).IsRequired();

                e.HasOne<Client>()
                 .WithMany()
                 .HasForeignKey(x => x.ClientId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
