using System;

namespace Microservice.Estoque.Models
{
    // Persist processed message ids for idempotency
    public class ProcessedMessage
    {
        public int Id { get; set; }
        public string MessageId { get; set; } = null!;
        public DateTime ProcessedAt { get; set; }
    }
}
