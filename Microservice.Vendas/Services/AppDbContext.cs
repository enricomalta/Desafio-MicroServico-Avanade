using Microsoft.EntityFrameworkCore;
using Microservice.Vendas.Models;

namespace Microservice.Vendas.Services
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Pedido> Pedidos { get; set; } // Tabela de pedidos
    public DbSet<PedidoItem> PedidoItens { get; set; } // Itens de pedido

        // Poderia-se configurar relacionamentos, precision de decimal etc. via OnModelCreating
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PedidoItem>()
                .Property(i => i.PrecoUnitario)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.ValorTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Pedido>()
                .HasMany(p => p.Itens)
                .WithOne(i => i.Pedido)
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}