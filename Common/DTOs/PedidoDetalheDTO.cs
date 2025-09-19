using System;
using System.Collections.Generic;

namespace Common.DTOs
{
    public class PedidoDetalheDTO
    {
        public int Id { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; } = 0m;
        public List<PedidoItemDTO> Itens { get; set; } = new();
    }

    public class PedidoItemDTO
    {
        public int ProdutoId { get; set; }
        public string? Nome { get; set; }          // Snapshot opcional do nome no momento da criação
        public decimal PrecoUnitario { get; set; } // Snapshot
        public int Quantidade { get; set; }
        public decimal Subtotal => PrecoUnitario * Quantidade;
    }
}