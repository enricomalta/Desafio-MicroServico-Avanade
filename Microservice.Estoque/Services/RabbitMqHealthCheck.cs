using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Microservice.Estoque.Services
{
    // Health check leve que tenta abrir uma conexão com RabbitMQ
    public class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly string _hostName;

        public RabbitMqHealthCheck(string hostName = "localhost")
        {
            _hostName = hostName;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var factory = new ConnectionFactory() { HostName = _hostName };
                using var conn = factory.CreateConnection();
                if (conn.IsOpen)
                    return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ disponível"));
                return Task.FromResult(HealthCheckResult.Unhealthy("Não foi possível abrir conexão com RabbitMQ"));
            }
            catch (System.Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Erro ao conectar RabbitMQ: " + ex.Message));
            }
        }
    }
}
