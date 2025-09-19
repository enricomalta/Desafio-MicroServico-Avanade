using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    // DTO de entrada para criação de pedido (cliente envia apenas itens mínimos)
    public class CreatePedidoDTO
    {
        [Required]
        public List<CreatePedidoItemDTO> Itens { get; set; } = new();
    }

    public class CreatePedidoItemDTO
    {
        [Required]
        public int ProdutoId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantidade { get; set; }
    }
}