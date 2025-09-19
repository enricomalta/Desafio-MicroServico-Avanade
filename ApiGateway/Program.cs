using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Common.Middleware;
using Common.Config;
using Common.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;


// Cria o builder padrão do ASP.NET Core (lê args, carrega appsettings, etc.)
var builder = WebApplication.CreateBuilder(args);

// Adiciona arquivo de configuração do Ocelot (definição das rotas para os microserviços downstream)
// reloadOnChange = true permite alterar o arquivo e recarregar sem reiniciar a aplicação
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Registra Ocelot no container de DI usando a configuração carregada
builder.Services.AddOcelot(builder.Configuration);

// Configura CORS (ajustar origens conforme necessidade futuramente)
builder.Services.AddCors(options =>
{
	options.AddPolicy("Default", policy =>
		policy.AllowAnyOrigin()
			  .AllowAnyHeader()
			  .AllowAnyMethod());
});

// Health checks para monitoramento
builder.Services.AddHealthChecks();

// Registrar Correlation middleware do Common
builder.Services.AddSingleton<Common.Middleware.CorrelationIdMiddleware>();

// Rate limiting simples em memória (por IP) será aplicado como middleware abaixo

// Configura autenticação JWT no gateway para filtrar antes de rotear
// Busca a chave pública primeiro em variáveis de ambiente e depois em configuration (appsettings)
var publicKeyPath = Environment.GetEnvironmentVariable("JWT_PUBLIC_KEY_PATH") ?? builder.Configuration["Jwt:PublicKeyPath"];
if (string.IsNullOrEmpty(publicKeyPath) || !File.Exists(publicKeyPath))
	throw new FileNotFoundException($"Public key not found at {publicKeyPath}");
var publicKey = File.ReadAllText(publicKeyPath);
var rsa = RSA.Create();
rsa.ImportFromPem(publicKey.ToCharArray());

builder.Services.AddAuthentication(options =>
{
	options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
	options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuer = true,
		ValidIssuer = builder.Configuration["Jwt:Issuer"],
		ValidateAudience = true,
		ValidAudience = builder.Configuration["Jwt:Audience"],
		ValidateLifetime = true,
		IssuerSigningKey = new RsaSecurityKey(rsa),
		ValidateIssuerSigningKey = true
	};
});

builder.Services.AddAuthorization(options =>
{
	// Exemplo de policy que exige role "admin"
	options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

// Registrar RabbitMqOptions caso o gateway precise encaminhar/inspecionar mensagens (opcional)
builder.Services.Configure<Common.Config.RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

var app = builder.Build();

// Pipeline básico. IMPORTANTE: Authentication/Authorization só terão efeito
// se forem configurados (por exemplo JWT bearer) — atualmente não há configuração aqui.
app.UseRouting();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();

// Usar CorrelationId middleware (padronizado)
app.UseMiddleware<Common.Middleware.CorrelationIdMiddleware>();

// Health check endpoint
app.MapHealthChecks("/health");

// Rate limiting simples em memória (por IP) — proteção básica
var requestCounts = new System.Collections.Concurrent.ConcurrentDictionary<string, (int Count, DateTime WindowStart)>();
app.Use(async (context, next) =>
{
	var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
	var now = DateTime.UtcNow;
	var entry = requestCounts.GetOrAdd(ip, _ => (0, now));
	if ((now - entry.WindowStart) > TimeSpan.FromMinutes(1))
	{
		entry = (0, now);
	}
	entry.Count++;
	requestCounts[ip] = entry;
	// Limite simples: 60 req/min por IP
	if (entry.Count > 60)
	{
		context.Response.StatusCode = 429;
		await context.Response.WriteAsync("Muitas requisições - limite de taxa excedido");
		return;
	}
	await next();
});

// Ocelot deve ser registrado ao final para inspecionar a requisição e rotear
await app.UseOcelot();

app.Run();
