using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    // DTO do Pedido — representa a visão exposta de um pedido, desacoplando do modelo de persistência.
    public class PedidoDTO
    {
        public int Id { get; set; } // Id do pedido

        [Required]
        public DateTime DataPedido { get; set; } = DateTime.UtcNow; // Momento da criação do pedido

        [Required]
        public List<ProdutoPedidoDTO> Produtos { get; set; } = new(); // Itens solicitados (apenas id e quantidade)

        [Required]
        public decimal ValorTotal { get; set; } = 0m; // Soma dos valores (poderia ser calculado no servidor para evitar fraude)
    }

    // Representa um item de pedido (referência a produto + quantidade).
    public class ProdutoPedidoDTO
    {
        public int ProdutoId { get; set; } // Referência ao produto
        public int Quantidade { get; set; } // Quantidade solicitada
    }
}