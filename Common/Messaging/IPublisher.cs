namespace Common.Messaging
{
    // Contrato simples para publicar mensagens de domínio.
    public interface IPublisher
    {
        System.Threading.Tasks.Task PublishAsync(string routingKey, string content, System.Collections.Generic.IDictionary<string, object>? headers = null);
    }
}
