namespace Common.DTOs
{
    // Contrato de mensagem para solicitar abatimento de estoque.
    // Publicado pelo serviço de Vendas quando um pedido é criado.
    // Consumido pelo serviço de Estoque para decrementar quantidades.
    public class EstoqueReservaMensagemDTO
    {
        public int ProdutoId { get; set; }
        public int Quantidade { get; set; }
    }
}
