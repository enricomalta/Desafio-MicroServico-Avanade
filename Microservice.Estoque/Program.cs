using Serilog;
using Microservice.Estoque.Services;
using Common.Config;
using Common.Middleware;
using Common.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

// Configuração inicial do Serilog: escreve em console e em arquivo diário (logs/log-<data>.txt)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5002");
builder.Host.UseSerilog();

// Registro de serviços (DI)
// As linhas comentadas indicam intenção anterior de usar Singleton, mas como o serviço depende de DbContext (scoped), usar Scoped é mais adequado.
// builder.Services.AddSingleton<EstoqueService>();
// builder.Services.AddSingleton<RabbitMqConsumerService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
    }));
builder.Services.AddScoped<EstoqueService>(); // Serviço principal de estoque
// Registrar consumidor de fila como HostedService
builder.Services.AddHostedService<RabbitMqConsumerService>();

// CORS básico (ajustar origens no futuro conforme front-end)
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Health checks: readiness (DB + RabbitMQ) e liveness
// Health checks: readiness (DB + RabbitMQ) e liveness
// Registrar health checks customizados
builder.Services.AddSingleton<RabbitMqHealthCheck>();
builder.Services.AddSingleton<DbHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq")
    .AddCheck<DbHealthCheck>("database");

// Registrar RabbitMQ options a partir da configuração (section: RabbitMq)
builder.Services.Configure<Common.Config.RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
// Registrar healthcheck configurado para usar host de configuração
builder.Services.AddSingleton<RabbitMqHealthCheck>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Common.Config.RabbitMqOptions>>();
    return new RabbitMqHealthCheck(opts.Value.HostName ?? "localhost");
});

// Configuração JWT (similar ao serviço de Vendas)
var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"];
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
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

// Controllers e Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Microservice.Estoque", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
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

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Mantido somente em ambiente de desenvolvimento; se quiser expor sempre, mover fora do if.
app.UseRouting();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Health endpoints separando liveness (apenas app rodando) e readiness (dependências)
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();
