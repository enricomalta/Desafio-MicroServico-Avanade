using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microservice.Estoque.Models;
using Microservice.Estoque.Services;

[ApiController] // Indica que o controller usa convenções automáticas de binding/validation
[Route("api/v1/[controller]")] // Agora expõe /api/v1/produtos para alinhar padronização e versionamento
[Authorize] // Protege endpoints com JWT
public class ProdutosController : ControllerBase
{
    private readonly EstoqueService _estoqueService;

    public ProdutosController(EstoqueService estoqueService)
    {
        _estoqueService = estoqueService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var produtos = await _estoqueService.GetProdutosAsync();
        return Ok(produtos); // 200 + lista de produtos
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Produto produto)
    {
        var novoProduto = await _estoqueService.AddProdutoAsync(produto);
        // Retorna 201 + localização (aqui usa Get sem id específico — poderia haver endpoint /produtos/{id})
        return CreatedAtAction(nameof(Get), new { id = novoProduto.Id }, novoProduto);
    }
}
