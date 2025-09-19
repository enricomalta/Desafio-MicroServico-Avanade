using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;

namespace Common.Middleware
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string HeaderName = "X-Correlation-ID";

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(HeaderName, out var correlation))
            {
                correlation = Guid.NewGuid().ToString();
                context.Request.Headers[HeaderName] = correlation;
            }

            context.Response.OnStarting(() => {
                // Use o indexador para setar o header (evita ArgumentException se a chave já existir)
                context.Response.Headers[HeaderName] = correlation.ToString();
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
