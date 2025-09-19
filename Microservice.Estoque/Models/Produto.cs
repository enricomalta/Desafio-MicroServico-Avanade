using Microservice.Estoque.Models;

namespace Microservice.Estoque.Models
{
    public class Produto
    {
        public int Id { get; set; }              // Chave primária
        public string? Nome { get; set; }         // Nome do produto
        public string? Descricao { get; set; }    // Descrição opcional
        public decimal Preco { get; set; }        // Valor unitário (recomenda-se definir precisão via Fluent API)
        public int Quantidade { get; set; }       // Quantidade atual em estoque
    }
}
