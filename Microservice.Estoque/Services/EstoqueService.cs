using Microsoft.Extensions.Logging;
using Microservice.Estoque.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Microservice.Estoque.Services
{
    public class EstoqueService
    {
        private readonly ILogger<EstoqueService> _logger; // Para logs de operações
        private readonly AppDbContext _context;           // DbContext EF Core

        public EstoqueService(ILogger<EstoqueService> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        // Retorna todos os produtos do banco
        public async Task<List<Produto>> GetProdutosAsync()
        {
            return await _context.Produtos.ToListAsync();
        }

        // Adiciona um novo produto e persiste
        public async Task<Produto> AddProdutoAsync(Produto produto)
        {
            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();
            return produto;
        }

        // Atualiza quantidade absoluta (define novaQuantidade)
        public async Task<bool> AtualizarQuantidadeAsync(int produtoId, int novaQuantidade)
        {
            var produto = await _context.Produtos.FindAsync(produtoId);
            if (produto == null) return false;
            produto.Quantidade = novaQuantidade;
            await _context.SaveChangesAsync();
            return true;
        }
        
        // Deduz quantidade (ex: após uma venda). Garante que não fica negativo.
        public async Task<bool> AtualizarEstoque(int produtoId, int quantidade)
        {
            var produto = await _context.Produtos.FindAsync(produtoId);
            if (produto == null) return false;
            produto.Quantidade -= quantidade;
            if (produto.Quantidade < 0) produto.Quantidade = 0; // Evita valores negativos
            await _context.SaveChangesAsync();
            return true;
        }
    }
}