using Xunit;

public class EstoqueServiceTests
{
    [Fact]
    public void Deve_Adicionar_Produto()
    {
        var service = new EstoqueService(null);
        var produto = new Produto { Nome = "Teclado", Quantidade = 10 };
        service.AddProduto(produto);
        Assert.Single(service.GetProdutos());
    }

    [Fact]
    public void Deve_Atualizar_Quantidade_Estoque()
    {
        var service = new EstoqueService(null);
        var produto = new Produto { Nome = "Mouse", Quantidade = 20 };
        service.AddProduto(produto);
        service.AtualizarEstoque(produto.Id, 15);
        Assert.Equal(15, service.GetProdutos()[0].Quantidade);
    }
}
