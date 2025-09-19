using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace Microservice.Estoque.Services
{
    // Health check que verifica conectividade com o banco via AppDbContext
    public class DbHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _provider;

        public DbHealthCheck(IServiceProvider provider)
        {
            _provider = provider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                return canConnect ? HealthCheckResult.Healthy("Banco acessível") : HealthCheckResult.Unhealthy("Banco inacessível");
            }
            catch (System.Exception ex)
            {
                return HealthCheckResult.Unhealthy("Erro ao verificar banco: " + ex.Message);
            }
        }
    }
}
