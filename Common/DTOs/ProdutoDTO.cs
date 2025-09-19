using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    // DTO utilizado para transferência de dados do Produto entre camadas / serviços.
    // Diferente do modelo de domínio, ideal para expor apenas o necessário ao cliente.
    public class ProdutoDTO
    {
        public int Id { get; set; } // Identificador (normalmente preenchido pelo banco)

        [Required] // Nome obrigatório
        public string Nome { get; set; } = string.Empty;

        [Required] // Preço obrigatório; poderia ser validado com Range para evitar valores negativos
        public decimal Preco { get; set; }

        [Required] // Quantidade em estoque obrigatória
        public int Quantidade { get; set; }
    }
}