namespace Common.Config
{
    // Opções de configuração para conexão com RabbitMQ
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? VirtualHost { get; set; }
        public int PublishRetryCount { get; set; } = 3;
        public int PublishRetryBaseDelayMs { get; set; } = 200;
    }
}
