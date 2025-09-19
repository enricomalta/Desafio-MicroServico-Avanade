using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microservice.Vendas.Services;
using Microservice.Vendas.Models;
using Common.DTOs;
using System.Linq;
using System.Collections.Generic;

namespace UnitTests
{
    public class VendasServiceTests
    {
        [Fact]
        public async Task CriarPedido_SavesPedidoAndPublishesMessage()
        {
            var options = new DbContextOptionsBuilder<Microservice.Vendas.Services.AppDbContext>()
                .UseInMemoryDatabase(databaseName: "VendasTestDb")
                .Options;

            using var context = new Microservice.Vendas.Services.AppDbContext(options);

            // Classe de teste que sobrescreve PublishMessageAsync para capturar a mensagem
            var service = new TestableVendasService(context);

            var dto = new CreatePedidoDTO
            {
                Itens = new List<CreatePedidoItemDTO> { new CreatePedidoItemDTO { ProdutoId = 1, Quantidade = 2 } }
            };

            var pedido = await service.CriarPedidoAsync(dto, correlationId: "corr-1");

            var saved = context.Pedidos.Include(p => p.Itens).FirstOrDefault();
            Assert.NotNull(saved);
            Assert.Equal(1, saved!.Itens.Count);

            // verificar que a mensagem publicada corresponde ao item
            Assert.Single(service.PublishedMessages);
            var published = service.PublishedMessages.First();
            Assert.Equal(1, published[0].ProdutoId);
            Assert.Equal(2, published[0].Quantidade);
        }

        // Helper subclass
        private class TestableVendasService : VendasService
        {
            public List<List<EstoqueReservaMensagemDTO>> PublishedMessages { get; } = new List<List<EstoqueReservaMensagemDTO>>();

            public TestableVendasService(Microservice.Vendas.Services.AppDbContext ctx) : base(ctx) { }

            protected override Task PublishMessageAsync(List<EstoqueReservaMensagemDTO> mensagem, string pedidoId, string? correlationId)
            {
                // capturar a mensagem em memória ao invés de enviar para RabbitMQ
                PublishedMessages.Add(mensagem);
                return Task.CompletedTask;
            }
        }
    }
}
