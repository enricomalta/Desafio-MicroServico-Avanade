using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;
using System.Linq;
using Common.DTOs;
using Common.Config;
using Common.Messaging;

namespace Microservice.Estoque.Services
{
    // HostedService que consome a fila 'estoque', aplica abatimentos e persiste IDs processados para idempotência
    public class RabbitMqConsumerService : BackgroundService
    {
        private readonly IServiceProvider _provider;
        private readonly ILogger<RabbitMqConsumerService> _logger;
        private IConnection? _connection;
        private readonly Common.Config.RabbitMqOptions _options;

        public RabbitMqConsumerService(IServiceProvider provider, ILogger<RabbitMqConsumerService> logger, Microsoft.Extensions.Options.IOptions<Common.Config.RabbitMqOptions> options)
        {
            _provider = provider;
            _logger = logger;
            _options = options?.Value ?? new Common.Config.RabbitMqOptions();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[Consumidor] Iniciando consumidor RabbitMQ...");
            var factory = new ConnectionFactory()
            {
                HostName = string.IsNullOrEmpty(_options.HostName) ? "localhost" : _options.HostName,
                Port = _options.Port,
                VirtualHost = string.IsNullOrEmpty(_options.VirtualHost) ? "/" : _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password
            };
            _connection = factory.CreateConnection();
            return base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var channel = _connection!.CreateModel();
            // Declarar fila principal com DLX apontando para fila de DLQ
            var args = new System.Collections.Generic.Dictionary<string, object?>();
            args["x-dead-letter-exchange"] = ""; // usa exchange default
            args["x-dead-letter-routing-key"] = "estoque-dlq";
            channel.QueueDeclare(queue: "estoque", durable: true, exclusive: false, autoDelete: false, arguments: args);
            // Declarar fila DLQ
            channel.QueueDeclare(queue: "estoque-dlq", durable: true, exclusive: false, autoDelete: false, arguments: null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                // declarar messageId/correlationId aqui para que o catch os veja
                string messageId = Guid.NewGuid().ToString();
                string? correlationId = null;
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    // Mensagem esperada: lista de EstoqueReservaMensagemDTO
                    var items = JsonConvert.DeserializeObject<System.Collections.Generic.List<EstoqueReservaMensagemDTO>>(json) ?? new System.Collections.Generic.List<EstoqueReservaMensagemDTO>();

                    var props = ea.BasicProperties;
                    messageId = props?.MessageId ?? messageId;
                    correlationId = props?.CorrelationId;
                    if (props?.Headers != null && props.Headers.ContainsKey("X-Correlation-ID") && correlationId == null)
                    {
                        try
                        {
                            var hdr = props.Headers["X-Correlation-ID"] as byte[];
                            if (hdr != null) correlationId = Encoding.UTF8.GetString(hdr);
                        }
                        catch { /* ignorar parsing */ }
                    }

                    _logger.LogInformation("[Consumidor] Recebida mensagem {MessageId} (CorrelationId={CorrelationId}) com {Count} itens", messageId, correlationId, items.Count);

                    using var scope = _provider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Idempotência: se já processado, ack e sair
                    if (db.ProcessedMessages.Any(pm => pm.MessageId == messageId))
                    {
                        _logger.LogWarning("[Consumidor] Mensagem {MessageId} já processada — ack e pular", messageId);
                        channel.BasicAck(ea.DeliveryTag, multiple: false);
                        return;
                    }

                    // Aplicar abatimento de estoque
                    foreach (var it in items)
                    {
                        var produto = db.Produtos.FirstOrDefault(p => p.Id == it.ProdutoId);
                        if (produto == null)
                        {
                            _logger.LogWarning("[Consumidor] Produto {ProdutoId} não encontrado — ignorando", it.ProdutoId);
                            continue;
                        }
                        produto.Quantidade -= it.Quantidade;
                        _logger.LogInformation("[Consumidor] Produto {ProdutoId} reduzido em {Quantidade}. Novo saldo: {NovoSaldo}", it.ProdutoId, it.Quantidade, produto.Quantidade);
                    }

                    db.ProcessedMessages.Add(new Models.ProcessedMessage { MessageId = messageId, ProcessedAt = DateTime.UtcNow });
                    await db.SaveChangesAsync();

                    _logger.LogInformation("[Consumidor] Mensagem {MessageId} processada com sucesso", messageId);
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Consumidor] Falha ao processar a mensagem — avaliando retry/DLQ");

                    try
                    {
                        // Verificar cabeçalho de retries
                        int retries = 0;
                        if (ea.BasicProperties?.Headers != null && ea.BasicProperties.Headers.ContainsKey("x-retries"))
                        {
                            var v = ea.BasicProperties.Headers["x-retries"];
                            if (v is byte[] b)
                            {
                                if (int.TryParse(Encoding.UTF8.GetString(b), out var parsed)) retries = parsed;
                            }
                            else if (v is int iv) retries = iv;
                        }

                        retries++;
                        var max = _options.PublishRetryCount > 0 ? _options.PublishRetryCount : 3;

                        // Se não excedeu max, republique na fila principal com header incrementado
                        if (retries <= max)
                        {
                            var propsRepublish = channel.CreateBasicProperties();
                            propsRepublish.Persistent = true;
                            propsRepublish.ContentType = ea.BasicProperties?.ContentType;
                            propsRepublish.MessageId = ea.BasicProperties?.MessageId ?? Guid.NewGuid().ToString();
                            propsRepublish.Headers = ea.BasicProperties?.Headers != null ? new System.Collections.Generic.Dictionary<string, object>(ea.BasicProperties.Headers) : new System.Collections.Generic.Dictionary<string, object>();
                            propsRepublish.Headers["x-retries"] = Encoding.UTF8.GetBytes(retries.ToString());
                            // Republish
                            channel.BasicPublish(exchange: "", routingKey: "estoque", basicProperties: propsRepublish, body: ea.Body.ToArray());
                            _logger.LogInformation("[Consumidor] Mensagem {MessageId} reenviada para retry {Retries}", messageId, retries);
                        }
                        else
                        {
                            // Enviar para DLQ
                            var propsDlq = channel.CreateBasicProperties();
                            propsDlq.Persistent = true;
                            propsDlq.ContentType = ea.BasicProperties?.ContentType;
                            propsDlq.MessageId = ea.BasicProperties?.MessageId ?? Guid.NewGuid().ToString();
                            propsDlq.Headers = ea.BasicProperties?.Headers != null ? new System.Collections.Generic.Dictionary<string, object>(ea.BasicProperties.Headers) : new System.Collections.Generic.Dictionary<string, object>();
                            propsDlq.Headers["x-original-retries"] = Encoding.UTF8.GetBytes(retries.ToString());
                            channel.BasicPublish(exchange: "", routingKey: "estoque-dlq", basicProperties: propsDlq, body: ea.Body.ToArray());
                            _logger.LogWarning("[Consumidor] Mensagem {MessageId} movida para DLQ após {Retries} tentativas", messageId, retries);
                        }
                        // Ack para remover a mensagem original
                        channel.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "[Consumidor] Falha ao tentar reenviar a mensagem para retry/DLQ");
                        // Não ack para permitir reentrega pela fila se algo deu errado
                    }
                }
            };

            channel.BasicConsume(queue: "estoque", autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[Consumidor] Parando consumidor RabbitMQ...");
            _connection?.Close();
            return base.StopAsync(cancellationToken);
        }
    }
}
