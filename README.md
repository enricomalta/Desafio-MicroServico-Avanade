# Microserviços - Estoque e Vendas

## Visão Geral
Este projeto é uma solução de microserviços simples construída em **.NET 9**, contendo:

- **ApiGateway (Ocelot)**: roteia requisições externas para os serviços internos e realiza validação de JWT antes de rotear (carrega `Jwt:PublicKeyPath` ou `JWT_PUBLIC_KEY_PATH`).
- **Microservice.Estoque**: CRUD de produtos e consumidor RabbitMQ (HostedService) para processar reservas de estoque; endpoints protegidos por JWT.
- **Microservice.Vendas**: criação e listagem de pedidos, protegido por JWT; publica eventos para abatimento de estoque na fila `estoque`.
- **Common**: DTOs e utilitários (incluindo `JwtHandler` para geração/validação RSA).
- **Mensageria**: RabbitMQ — publisher em Vendas e consumer em Estoque; filas e mensagens configuradas como duráveis/persistentes; consumer implementa retry, DLQ e idempotência.
- **Persistência**: EF Core, cada serviço possui seu próprio `AppDbContext`.
- **Logging**: Serilog integrado em Estoque e Vendas.

> Todos os arquivos principais incluem comentários explicativos sem alterar a lógica existente.

---

## Fluxo Principal de Requisição

1. **Cliente faz chamada** ao ApiGateway:  
   Ex: `GET /estoque/api/produtos` ou `POST /vendas/api/pedidos`.
2. **Ocelot lê `ocelot.json`** e encaminha para o serviço interno correspondente:
   - **Estoque**: `http://localhost:5002/api/produtos`  
     *(Controllers usam `[Route("[controller]")]` => `/produtos`, não `/api/produtos`)*
   - **Vendas**: `http://localhost:5003/api/pedidos`  
     *(Mesma questão de rota: `/pedidos` vs `/api/pedidos`)*
3. **Autenticação JWT**:
   - Microservice.Vendas exige token JWT.
   - Microservice.Estoque exige token JWT (configurado no serviço) e validação de token via chave pública.
   - ApiGateway também carrega a chave pública e valida JWT (autenticação no gateway). Isso permite validação centralizada de tokens antes de rotear.
4. **Criação de Pedido**:
   - Pedido salvo no banco.
   - Lista de produtos publicada na fila `estoque` do RabbitMQ (fila declarada como durável e mensagens publicadas como persistentes).
   - Consumo da fila pelo Microservice.Estoque está implementado por `RabbitMqConsumerService` (HostedService). O consumidor implementa idempotência (tabela ProcessedMessages), retry e DLQ (`estoque-dlq`).

---

## Arquitetura de Componentes

### ApiGateway
- Configuração de rotas no `ocelot.json`.
- Valida JWT (carrega `Jwt:PublicKeyPath` ou variável `JWT_PUBLIC_KEY_PATH`) e aplica autenticação antes de rotear.
- Responsável por centralizar roteamento para os microserviços internos.

### Common
- **JwtHandler**: Geração e validação de tokens RSA.
- **DTOs**: Abstraem modelos de domínio (ainda não aplicados nos controllers).

### Microservice.Estoque
- Endpoints: `/produtos` (GET, POST).
- Persistência via EF Core (`Produto`).
- Sem autenticação (lacuna de segurança atual).

### Microservice.Vendas
- Endpoints: `/pedidos` (GET, POST) com JWT obrigatório.
- Salva pedidos e publica produtos no RabbitMQ.
- Modelo de Pedido contém lista direta de `Produto` (alto acoplamento).

### Mensageria
- **RabbitMQ**: Publicação de eventos do Vendas para Estoque.
- Filas e mensagens configuradas como duráveis/persistentes.
- Consumo pelo Estoque implementado (HostedService), com retry e DLQ.
- Conexão atualmente criada por publicação/consumo; recomendação: otimizar reuso de conexão (singleton) em produção.

---

## Pontos de Melhoria e Segurança

1. **Roteamento inconsistente**: Padronizar `[Route("api/[controller]")]`.
2. **Autenticação no ApiGateway**: Validar JWT centralmente.
3. **Autorização no Estoque**: Incluir JWT.
4. **Acoplamento entre domínios**: Criar `PedidoItem` para evitar dependência direta de `Produto`.
5. **Validação de modelo**: Usar DTOs e AutoMapper.
6. **Validação de regras de negócio**: Verificar disponibilidade de estoque antes de criar pedido.
7. **Mensageria**: Consumidor RabbitMQ implementado; avaliar outbox para garantir atomicidade entre DB e mensageria.
8. **Filas duráveis**: Já configuradas no código; confirmar em ambiente de produção.
9. **Tratamento de erros e padrão outbox**: Evitar inconsistência entre banco e mensageria.
10. **Observabilidade**: Adicionar logs de negócio, correlation ID, health checks, versionamento de API, CORS, políticas de retry e segurança de conexão SQL.

---

## Recomendações Prioritárias (Top 10)

1. Ajustar rotas para alinhar com Ocelot.
2. Implementar JWT no ApiGateway.
3. Usar DTOs nos controllers (AutoMapper).
4. Implementar consumidor RabbitMQ no Estoque.
5. Refatorar Pedido para `PedidoItem` desacoplado de Produto.
6. Calcular `ValorTotal` no backend.
7. Configurar precisão decimal e validações de domínio.
8. Adicionar logs de negócio e correlation ID.
9. Criar testes unitários e de integração (pedido → fila).
10. Tornar filas duráveis e aplicar padrão outbox.

---

## Fluxo Detalhado (Pedido → Estoque)

1. Cliente envia `POST /vendas/api/pedidos` com token JWT válido.
2. Vendas valida token, salva pedido no banco.
3. Lista de produtos é serializada e publicada na fila `estoque`.
4. Estoque consome fila, atualiza quantidades, registra processamento (idempotência) e publica DLQ se necessário.
5. Vendas pode atualizar status do pedido (fluxo de compensação/outbox a ser implementado para robustez).

---

## Observações Técnicas

- Todos os arquivos principais possuem comentários explicativos.
- **JWT**: Armazenamento seguro recomendado (Key Vault/variáveis de ambiente), rotação de chaves.
- **Decimal/DateTime**: Usar precisão e `UtcNow`.
- **Testes**: Ainda não há cobertura completa de fluxo.
- **Health Checks e observabilidade**: Recomendados para SQL, RabbitMQ e gateway.

---

## Próximos Passos

- Autenticação centralizada no ApiGateway.
- Refatoração para DTOs e camada de Application (Commands/Handlers).
- Implementação de health checks e observabilidade.
- Versionamento de API (`/api/v1/...`).
- CI/CD para deploy contínuo.
- Introdução de políticas de retry, circuit-breaker, rate limiting e segurança de filas.

---

## Nota importante: Demo vs Produção

Este repositório contém configurações e chaves que facilitam a execução local e demonstração. Para fins de demonstração foram adotadas escolhas que NÃO são seguras para um ambiente de produção e **devem** ser modificadas antes de qualquer deploy público:

- Chaves privadas e credenciais (ex.: `ApiGateway/Keys/private.key`, `Tests/JWT/private.key`, `docker-compose.yml` com `SA_PASSWORD`) não devem ficar em um repositório público. Em produção, utilize um Secret Manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) ou mecanismos de secrets do provedor.
- Variáveis como `JWT_PUBLIC_KEY_PATH` podem apontar para arquivos locais durante a demo; em produção, configure o serviço para buscar chaves do Secret Manager ou de um endpoint de JWKS.
- CORS está configurado com `AllowAnyOrigin` para facilitar testes locais — em produção restrinja para os domínios do frontend.
- O rate limiter é uma implementação simples em memória adequada somente para demo; em produção use uma solução distribuída (ex: Redis, API Gateway gerenciado).
- Não deixe senhas (ex.: `SA_PASSWORD`) hard-coded em arquivos versionados. Use Docker secrets ou variáveis de ambiente injetadas pela pipeline CI/CD.

No README e nos exemplos mantemos essas configurações apenas para facilitar a execução local e a apresentação. Em uma implantação real, removeríamos todos os segredos do repositório, documentaríamos como gerar e provisionar chaves e ajustaríamos as políticas de CORS e rate limiting conforme o ambiente.

Observação: parte dessas ações já possuem implementação parcial no código (ex.: autenticação no gateway, consumer no Estoque, health checks). Priorizar outbox e administração de secrets para produção.

---


## Como rodar os testes unitários (rápido)

Requisitos:
- .NET 9 SDK instalado

Para executar os testes unitários criados no diretório `Tests/UnitTests` execute:

```powershell
cd d:\Dados\Coding\DIO\desafio
dotnet test "Tests\UnitTests\UnitTests.csproj"
```

Os testes usam o provedor InMemory do EF Core e não dependem de serviços externos.


---

**Autor:** Enrico Malta  
**Tecnologias:** .NET 7, EF Core, Serilog, RabbitMQ, Ocelot, JWT (RSA)

Observação: O projeto foi atualizado para .NET 9 (veja `global.json`).

---

## Configuração segura e execução local (variáveis de ambiente)

Recomendado: não commitar chaves privadas ou credenciais em repositórios. Use variáveis de ambiente ou um Secret Manager.

Variáveis de ambiente suportadas (exemplos):

- JWT_PUBLIC_KEY_PATH: caminho para o arquivo PEM com a chave pública (usado por Vendas, Estoque e ApiGateway)
- RABBITMQ__HOSTNAME, RABBITMQ__PORT, RABBITMQ__USERNAME, RABBITMQ__PASSWORD: configuração para section `RabbitMq` (duplo underscore mapeia para IConfiguration)
- ASPNETCORE_ENVIRONMENT: `Development` | `Production`

Executando localmente com Docker (RabbitMQ + SQL Server):

```powershell
cd d:\Dados\Coding\DIO\desafio
docker-compose up -d

# Exportar variáveis (PowerShell example)
$env:JWT_PUBLIC_KEY_PATH = "C:\path\to\public.key"
$env:RABBITMQ__HOSTNAME = "localhost"
$env:RABBITMQ__PORT = "5672"
$env:RABBITMQ__USERNAME = "guest"
$env:RABBITMQ__PASSWORD = "guest"

# Build + run serviços (em terminais separados)
dotnet build EcommerceMicroservices.sln
dotnet run --project Microservice.Estoque\Microservice.Estoque.csproj
dotnet run --project Microservice.Vendas\Microservice.Vendas.csproj
dotnet run --project ApiGateway\ApiGateway.csproj

# Após testar, derrubar containers
docker-compose down
```

Recomendações para produção:

- Use um Secret Manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) para armazenar chaves e credenciais.
- Configure pipeline CI/CD para injetar segredos de forma segura no ambiente de runtime.
- Planeje rotação de chaves e suporte a múltiplas chaves (para rollover sem downtime).

