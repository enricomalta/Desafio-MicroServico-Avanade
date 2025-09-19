using Xunit;
using System.Collections.Generic;

public class VendasServiceTests
{
    [Fact]
    public void Deve_Criar_Pedido()
    {
        var service = new VendasService();
        var pedido = new Pedido { Produtos = new List<Produto>{ new Produto { Id = 1, Nome = "Produto Teste", Quantidade=2 } } };
        service.CriarPedido(pedido);
        Assert.Single(service.GetPedidos());
        Assert.Equal("Pendente", service.GetPedidos()[0].Status);
    }
}
