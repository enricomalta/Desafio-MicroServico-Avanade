using Microsoft.AspNetCore.Mvc;
using Microservice.Vendas.Models;
using Microservice.Vendas.Services;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using Common.DTOs;
using System.Linq;

namespace Microservice.Vendas.Controllers
{
    [ApiController] // Ativa binding, validação automática e respostas de erro de modelo
    [Route("api/v1/[controller]")] // Nova rota versionada: /api/v1/pedidos
    [Authorize] // Requer token JWT válido (configurado em Program.cs)
    public class PedidosController : ControllerBase
    {
        private readonly VendasService _vendasService;

        public PedidosController(VendasService vendasService)
        {
            _vendasService = vendasService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var pedidos = await _vendasService.GetPedidosAsync();
            var resposta = pedidos.Select(p => _vendasService.MapToDetalhe(p));
            return Ok(resposta); // 200 OK com lista detalhada
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreatePedidoDTO dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            // Obtém o correlation id do cabeçalho, se presente, e o passa adiante para publicação na fila
            var correlationId = Request.Headers.ContainsKey("X-Correlation-ID") ? Request.Headers["X-Correlation-ID"].ToString() : null;
            var pedido = await _vendasService.CriarPedidoAsync(dto, correlationId);
            var detalhe = _vendasService.MapToDetalhe(pedido);
            return CreatedAtAction(nameof(Get), new { id = detalhe.Id }, detalhe);
        }
    }
}
