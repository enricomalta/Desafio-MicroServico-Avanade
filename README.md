# Microserviços - Estoque e Vendas

## Visão Geral
Este projeto é uma solução de microserviços em **.NET 9**, com arquitetura moderna e práticas reais de produção:

- **ApiGateway (Ocelot)**: roteia requisições externas, valida JWT (RS256) antes de rotear, aplica rate limiting simples e centraliza rotas (`ocelot.json`).
- **Microservice.Estoque**: CRUD de produtos, consumidor RabbitMQ (HostedService) para processar reservas de estoque, endpoints protegidos por JWT, idempotência e DLQ.
- **Microservice.Vendas**: criação/listagem de pedidos, protegido por JWT, publica eventos para abatimento de estoque na fila `estoque` (RabbitMQ).
- **Common**: DTOs, utilitários, `JwtHandler` para geração/validação de tokens RSA.
- **Mensageria**: RabbitMQ — publisher em Vendas, consumer em Estoque; filas/mensagens duráveis/persistentes, retry, DLQ e idempotência implementados.
- **Persistência**: EF Core, cada serviço com seu próprio `AppDbContext`.
- **Logging**: Serilog integrado em Estoque e Vendas.

> Todos os arquivos principais possuem comentários explicativos e instruções de uso.

---

## Fluxo Principal de Requisição

1. Cliente faz chamada ao ApiGateway (ex: `POST /api/v1/pedidos`).
2. ApiGateway valida JWT e encaminha para o serviço correto.
3. Microservice.Vendas cria o pedido, salva no banco e publica mensagem na fila `estoque` (RabbitMQ).
4. Microservice.Estoque consome a fila, aplica abatimento, registra processamento (idempotência) e publica em DLQ se necessário.

---

## Arquitetura de Componentes

### ApiGateway
- Configuração de rotas no `ocelot.json`.
- Valida JWT (carrega `Jwt:PublicKeyPath` ou variável `JWT_PUBLIC_KEY_PATH`) e aplica autenticação antes de rotear.
- Responsável por centralizar roteamento para os microserviços internos.

### Common
- **JwtHandler**: Geração e validação de tokens RSA.
- **DTOs**: Abstraem modelos de domínio (usados nos controllers para evitar over-posting).

### Microservice.Estoque
- Endpoints: `/api/v1/produtos` (GET, POST), protegidos por JWT.
- Persistência via EF Core (`Produto`).

### Microservice.Vendas
- Endpoints: `/api/v1/pedidos` (GET, POST) com JWT obrigatório.
- Salva pedidos e publica eventos de reserva de estoque no RabbitMQ.
- Modelo de Pedido desacoplado via DTOs.

### Mensageria
- **RabbitMQ**: Publisher em Vendas, consumer em Estoque; filas/mensagens duráveis/persistentes, retry, DLQ e idempotência implementados.
- Recomendação: otimizar reuso de conexão (singleton) em produção.

---

## Pontos de Melhoria e Segurança

1. Padronizar rotas e versionamento (`/api/v1/...`).
	- ✔️ Implementado: Todos os endpoints usam `/api/v1/...`.
2. Validar JWT centralmente no ApiGateway e nos serviços.
	- ✔️ Implementado: ApiGateway valida JWT antes de rotear; Estoque e Vendas exigem JWT.
3. Usar DTOs nos controllers para evitar over-posting.
	- ✔️ Implementado: Controllers usam DTOs para entrada/saída.
4. Implementar padrão outbox para garantir atomicidade entre DB e mensageria.
	- ⏳ Parcial: Idempotência e DLQ implementados; padrão outbox está documentado como recomendação.
5. Adicionar testes de integração end-to-end.
	- ⏳ Parcial: Testes unitários implementados; integração recomendada.
6. Restringir CORS e proteger endpoints sensíveis.
	- ⏳ Parcial: CORS aberto para testes locais; restrição recomendada para produção.
7. Monitorar e rotacionar chaves JWT.
	- ⏳ Parcial: Suporte a rotação previsto; rotação automática não implementada.
8. Adicionar logs de negócio e correlation ID.
	- ✔️ Implementado: Serilog integrado; correlation ID middleware presente.
9. Configurar pipeline CI/CD para build/test/deploy seguro.
	- ⏳ Parcial: Documentado como recomendação; pipeline não presente no repositório.

---

## Recomendações Prioritárias (Top 10)

1. Padronizar rotas e versionamento. ✔️
2. Validar JWT no ApiGateway e nos serviços. ✔️
3. Usar DTOs nos controllers. ✔️
4. Implementar padrão outbox. ⏳ Parcial
5. Adicionar testes de integração. ⏳ Parcial
6. Restringir CORS em produção. ⏳ Parcial
7. Monitorar e rotacionar chaves JWT. ⏳ Parcial
8. Adicionar logs de negócio e correlation ID. ✔️
9. Configurar pipeline CI/CD. ⏳ Parcial

---

## Fluxo Detalhado (Pedido → Estoque)

1. Cliente envia `POST /api/v1/pedidos` com token JWT válido.
2. Vendas valida token, salva pedido no banco e publica mensagem na fila `estoque`.
3. Estoque consome fila, atualiza quantidades, registra processamento (idempotência), faz retry e publica em DLQ se necessário.
4. Vendas pode atualizar status do pedido (compensação/outbox).

---

## Observações Técnicas

- Todos os arquivos principais possuem comentários explicativos.
- **JWT**: Armazenamento seguro recomendado (Key Vault/variáveis de ambiente), rotação de chaves.
- **Decimal/DateTime**: Usar precisão e `UtcNow`.
- **Testes**: Cobertura unitária e recomenda-se ampliar para integração.
- **Health Checks e observabilidade**: Implementados para SQL, RabbitMQ e gateway.

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
cd C:\path\to\Desafio-MicroServico-Avanade
dotnet test "Tests\UnitTests\UnitTests.csproj"
```

Os testes usam o provedor InMemory do EF Core e não dependem de serviços externos.


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

---

**Autor:** Enrico Malta  
**Tecnologias:** .NET 9, EF Core, Serilog, RabbitMQ, Ocelot, JWT (RSA)

---
