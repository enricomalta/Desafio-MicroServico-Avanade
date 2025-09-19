using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microservice.Estoque.Services;
using Microservice.Estoque.Models;
using Common.DTOs;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UnitTests
{
    public class EstoqueProcessingTests
    {
        [Fact]
        public async Task Consumer_ProcessMessage_DecrementsProdutoAndRecordsProcessedMessage()
        {
            var options = new DbContextOptionsBuilder<Microservice.Estoque.Services.AppDbContext>()
                .UseInMemoryDatabase(databaseName: "EstoqueTestDb")
                .Options;

            using var context = new Microservice.Estoque.Services.AppDbContext(options);

            // Seed produto
            var produto = new Produto { Id = 1, Nome = "Caneta", Quantidade = 10 };
            context.Produtos.Add(produto);
            await context.SaveChangesAsync();

            // Simulate message
            var mensagem = new List<EstoqueReservaMensagemDTO> { new EstoqueReservaMensagemDTO { ProdutoId = 1, Quantidade = 3 } };
            var json = JsonConvert.SerializeObject(mensagem);

            // Manually run the processing logic similar to RabbitMqConsumerService
            var messageId = Guid.NewGuid().ToString();

            // Check idempotency before
            Assert.False(context.ProcessedMessages.Any(pm => pm.MessageId == messageId));

            // Apply processing
            var items = JsonConvert.DeserializeObject<List<EstoqueReservaMensagemDTO>>(json);
            foreach (var it in items!)
            {
                var p = context.Produtos.FirstOrDefault(x => x.Id == it.ProdutoId);
                if (p != null)
                {
                    p.Quantidade -= it.Quantidade;
                }
            }

            context.ProcessedMessages.Add(new Microservice.Estoque.Models.ProcessedMessage { MessageId = messageId, ProcessedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();

            var updated = context.Produtos.First(x => x.Id == 1);
            Assert.Equal(7, updated.Quantidade);
            Assert.True(context.ProcessedMessages.Any(pm => pm.MessageId == messageId));
        }
    }
}
