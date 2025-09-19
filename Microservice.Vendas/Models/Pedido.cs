using System;
using System.Collections.Generic;

namespace Microservice.Vendas.Models
{
    public class Pedido
    {
        public int Id { get; set; }                               // Chave primária
        public List<PedidoItem> Itens { get; set; } = new();       // Nova lista de itens desacoplada de Produto
        public string Status { get; set; } = "Pendente";          // Status do pedido (ex: Pendente, Processado, Cancelado)
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;  // Timestamp criação em UTC
        public decimal ValorTotal { get; set; }                    // Armazena snapshot do valor total
    }

    public class PedidoItem
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public int ProdutoId { get; set; }
        public string? Nome { get; set; }
        public decimal PrecoUnitario { get; set; }
        public int Quantidade { get; set; }
    public Pedido? Pedido { get; set; }
    }
}
