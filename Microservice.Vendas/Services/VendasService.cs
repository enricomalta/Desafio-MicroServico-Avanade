using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Microservice.Vendas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Common.DTOs;
using Common.Messaging;
using System.Linq;
using System;

namespace Microservice.Vendas.Services
{
    public class VendasService
    {
        private readonly AppDbContext _context;      // DbContext para persistir pedidos
        private readonly Common.Messaging.IPublisher _publisher;

        // Construtor: injeta AppDbContext e obrigatoriamente um IPublisher
        public VendasService(AppDbContext context, Common.Messaging.IPublisher publisher)
        {
            _context = context;
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        }

        // Overload útil para testes que não desejam configurar um publisher real.
        // Usa um NoOpPublisher interno que não envia mensagens.
        public VendasService(AppDbContext context) : this(context, new NoOpPublisher()) { }

        // Retorna todos os pedidos registrados
        public async Task<List<Pedido>> GetPedidosAsync()
        {
            // Inclui itens para retorno completo
            return await _context.Pedidos.Include(p => p.Itens).ToListAsync();
        }

        // Cria pedido a partir de DTO de entrada, calcula valor total e publica mensagem minimalista
        public async Task<Pedido> CriarPedidoAsync(CreatePedidoDTO dto)
        {
            return await CriarPedidoAsync(dto, correlationId: null);
        }

        // Sobrecarga que aceita correlationId opcional para propagar na mensageria
        public async Task<Pedido> CriarPedidoAsync(CreatePedidoDTO dto, string? correlationId)
        {
            if (dto.Itens == null || dto.Itens.Count == 0)
                throw new ArgumentException("Pedido deve conter ao menos um item.");

            // Futuro: validar produtos consultando serviço de estoque (HTTP ou cache) — placeholder aqui
            // Por enquanto iremos simular precificação fixa (ex: 10.00 cada) ou buscar tabela local se existisse.
            // Para não deixar estático, poderíamos levantar exceção para forçar implementação futura.

            var pedido = new Pedido
            {
                Status = "Pendente",
                DataCriacao = DateTime.UtcNow
            };

            foreach (var item in dto.Itens)
            {
                // Placeholder: preço fictício. Em cenário real, buscar do estoque ou pricing service.
                decimal precoSnapshot = 10.00m; 
                pedido.Itens.Add(new PedidoItem
                {
                    ProdutoId = item.ProdutoId,
                    Quantidade = item.Quantidade,
                    PrecoUnitario = precoSnapshot,
                    Nome = null // poderia ser resolvido futuramente via chamada ao serviço de estoque
                });
            }

            pedido.ValorTotal = pedido.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            // Publica mensagem com lista reduzida (ProdutoId, Quantidade) para abatimento
            var mensagem = pedido.Itens.Select(i => new EstoqueReservaMensagemDTO { ProdutoId = i.ProdutoId, Quantidade = i.Quantidade }).ToList();

            // Delegar publicação a um método protegido (testável/overridable)
            await PublishMessageAsync(mensagem, pedido.Id.ToString(), correlationId);

            return pedido;
        }

        public PedidoDetalheDTO MapToDetalhe(Pedido pedido)
        {
            return new PedidoDetalheDTO
            {
                Id = pedido.Id,
                DataCriacao = pedido.DataCriacao,
                Status = pedido.Status,
                ValorTotal = pedido.ValorTotal,
                Itens = pedido.Itens.Select(i => new PedidoItemDTO
                {
                    ProdutoId = i.ProdutoId,
                    Nome = i.Nome,
                    PrecoUnitario = i.PrecoUnitario,
                    Quantidade = i.Quantidade
                }).ToList()
            };
        }

        // Nota: Publicação de mensagens delegada ao IPublisher injetado.
        // Método protegido que realiza a publicação. Testes podem sobrescrever este método.
        protected virtual async Task PublishMessageAsync(List<EstoqueReservaMensagemDTO> mensagem, string pedidoId, string? correlationId)
        {
            var content = JsonConvert.SerializeObject(mensagem);
            var headers = new System.Collections.Generic.Dictionary<string, object>();
            if (!string.IsNullOrEmpty(correlationId)) headers["X-Correlation-ID"] = correlationId!;
            await _publisher.PublishAsync("estoque", content, headers);
        }

        // Implementação interna de IPublisher que não faz nada — usada apenas para testes simples.
        private class NoOpPublisher : Common.Messaging.IPublisher
        {
            public System.Threading.Tasks.Task PublishAsync(string routingKey, string content, System.Collections.Generic.IDictionary<string, object>? headers = null)
            {
                // Não publica nada em ambiente de teste
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }
    }
}
