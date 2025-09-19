using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Messaging;
using Common.Config;
using Microsoft.Extensions.Options;
using System;

namespace Microservice.Vendas.Services
{
    // Implementação de IPublisher usando RabbitMQ com retry exponencial simples.
    public class RabbitMqPublisher : IPublisher
    {
        private readonly Common.Config.RabbitMqOptions _options;

        public RabbitMqPublisher(Microsoft.Extensions.Options.IOptions<Common.Config.RabbitMqOptions> options)
        {
            _options = options.Value;
        }

        public async Task PublishAsync(string routingKey, string content, IDictionary<string, object>? headers = null)
        {
            var factory = new ConnectionFactory() { HostName = _options.HostName, Port = _options.Port };
            if (!string.IsNullOrEmpty(_options.UserName)) factory.UserName = _options.UserName;
            if (!string.IsNullOrEmpty(_options.Password)) factory.Password = _options.Password;
            if (!string.IsNullOrEmpty(_options.VirtualHost)) factory.VirtualHost = _options.VirtualHost;

            int attempt = 0;
            var body = Encoding.UTF8.GetBytes(content);

            while (true)
            {
                try
                {
                    using var connection = factory.CreateConnection();
                    using var channel = connection.CreateModel();
                    channel.QueueDeclare(queue: routingKey, durable: true, exclusive: false, autoDelete: false, arguments: null);

                    var props = channel.CreateBasicProperties();
                    props.Persistent = true;
                    props.ContentType = "application/json";
                    props.MessageId = Guid.NewGuid().ToString();
                    if (headers != null)
                        props.Headers = new Dictionary<string, object>(headers);

                    channel.BasicPublish(exchange: "", routingKey: routingKey, basicProperties: props, body: body);
                    return;
                }
                catch (Exception)
                {
                    attempt++;
                    if (attempt >= Math.Max(1, _options.PublishRetryCount)) throw;
                    var delay = _options.PublishRetryBaseDelayMs * Math.Pow(2, attempt - 1);
                    await Task.Delay((int)delay);
                }
            }
        }
    }
}
