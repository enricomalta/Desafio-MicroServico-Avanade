using Microsoft.EntityFrameworkCore;
using Microservice.Estoque.Models;

namespace Microservice.Estoque.Services
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<Microservice.Estoque.Models.ProcessedMessage> ProcessedMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Define precisão para valores monetários para evitar truncamentos (warning original do EF)
            modelBuilder.Entity<Produto>()
                .Property(p => p.Preco)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Microservice.Estoque.Models.ProcessedMessage>()
                .Property(pm => pm.MessageId)
                .HasMaxLength(200);

            base.OnModelCreating(modelBuilder);
        }
    }
}