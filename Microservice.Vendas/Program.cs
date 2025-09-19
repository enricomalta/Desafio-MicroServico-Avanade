using Serilog;
using Microservice.Vendas.Services;
using Common.Messaging;
using Common.Config;
using Common.Middleware;
using Common.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;


// Configuração de logging usando Serilog (console + arquivos diários)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5003");
builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
    }));

// Carrega chave pública para validar tokens JWT (RS256)
// Primeira tentativa: variável de ambiente JWT_PUBLIC_KEY_PATH; fallback para appsettings
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
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,             // Garante expiração
        IssuerSigningKey = new RsaSecurityKey(rsa),
        ValidateIssuerSigningKey = true      // Garante que a assinatura RSA é válida
        // Pode-se adicionar ClockSkew se necessário
    };
});

builder.Services.AddScoped<VendasService>();

// Configurar opções de RabbitMQ a partir de configuration (section "RabbitMq")
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
// Registrar publisher RabbitMQ para publicação de mensagens (injeção de dependência)
builder.Services.AddSingleton<IPublisher, RabbitMqPublisher>();

// CORS (padronizado com os outros serviços)
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});
builder.Services.AddControllers();
// Correlation ID middleware from Common
builder.Services.AddSingleton<Common.Middleware.CorrelationIdMiddleware>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Microservice.Vendas", Version = "v1", Description = "API de Vendas do exemplo de microserviços (documentação em Português)." });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Header de autorização JWT no formato Bearer. Exemplo: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseCors("Default");
app.UseMiddleware<Common.Middleware.CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
