# 🛡️ FloodShield — API de Abrigos e Pontos de Apoio

> **Predict. Protect. Save Lives.**
> Global Solution 2026/2 · FIAP · Disciplina **Advanced Business Development with .NET** · Tema **CitizenTech**

## 1. Sobre o projeto

O **FloodShield** é uma plataforma de prevenção de enchentes e proteção da população. Esta API em **.NET** é o *backend complementar* de **abrigos e pontos de apoio**, consumido pelo **app Mobile (React Native)** da solução e pelo painel administrativo.

**Valor real ao cidadão:** em uma situação de alagamento, o morador consulta `GET /shelters/nearby` e recebe os abrigos **abertos e com vaga** mais próximos da sua localização, ordenados por distância (fórmula de Haversine). O administrador (defesa civil/prefeitura) mantém os abrigos e a ocupação atualizados em tempo real.

**ODS relacionados:** ODS 9 (Indústria, Inovação e Infraestrutura), ODS 11 (Cidades e Comunidades Sustentáveis) e ODS 13 (Ação contra a Mudança Global do Clima).

## 2. Integrantes

Guilherme de Andrade Martini — RM 566087  
Bruno Vinicius Barbosa — RM 566366 
Nathan Gonçalves Pereira Mendes — RM 564666 
Raphael Gomes Mancera — RM 562279 

## 3. Tecnologias

- .NET 8 · ASP.NET Core **Minimal APIs**
- **EF Core 8** + **SQL Server** (LocalDB ou Docker; compatível com Azure SQL)
- **JWT Bearer** (autenticação e autorização por papéis: `Admin` e `Citizen`)
- **Swagger / OpenAPI** (Swashbuckle) com botão *Authorize*
- **Rate Limiting** nativo do ASP.NET Core (HTTP 429)
- **Idempotência** por header `Idempotency-Key`
- **Health Checks** (API, banco de dados e backend Java)
- **xUnit** (unitários e integração com `WebApplicationFactory`)

## 4. Arquitetura

```
src/FloodShield.Shelters.Api/
├── Endpoints/       Minimal APIs (AuthEndpoints, ShelterEndpoints) + políticas
├── Domain/          Entidades e regras de negócio (Shelter, User, IdempotencyRecord)
├── Data/            AppDbContext (EF Core) e DbSeeder
├── Services/        ShelterService, TokenService, GeoCalculator (Haversine), settings
├── Contracts/       DTOs de entrada/saída com validação (DataAnnotations)
├── Infrastructure/  IdempotencyFilter, tratamento global de erros, filtros do Swagger
├── HealthChecks/    Verificação do backend Java + writer JSON
└── Program.cs       Composição (JWT, RateLimiter, HealthChecks, Swagger)
tests/FloodShield.Shelters.Tests/
├── Unit/            Regras de domínio, Haversine, token, validação
└── Integration/     Endpoints reais em memória (401/403/201/422/429...)
```

## 5. Como executar

### Pré-requisitos
.NET SDK 8+ e **SQL Server** (uma das opções abaixo).

### 5.1 Banco de dados

**Opção A — Windows com LocalDB** (já é o padrão do `appsettings.json`; nada a configurar).

**Opção B — Docker (qualquer SO):**
```bash
cp .env.example .env            # edite e defina MSSQL_SA_PASSWORD (senha forte)
docker compose up -d
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=FloodShieldShelters;User Id=sa;Password=<SUA_SENHA>;TrustServerCertificate=True" --project src/FloodShield.Shelters.Api
```

**Opção C — Azure SQL:** defina a variável de ambiente `ConnectionStrings__Default` com a connection string do banco.

As tabelas são criadas automaticamente na primeira execução (`EnsureCreated`).

### 5.2 Rodando a API
```bash
dotnet run --project src/FloodShield.Shelters.Api
```
Abra **http://localhost:5080/swagger**.

Em **Development**, ao subir pela primeira vez a API:
- cria 3 abrigos de exemplo;
- cria o usuário **admin** (`admin@floodshield.local`) com uma **senha aleatória exibida no console** (nada de credencial fixa no código).
- usa uma chave JWT efêmera se `Jwt:Key` não estiver definida.

Para uma senha de admin e uma chave JWT fixas, use *user-secrets* (não vão para o Git):
```bash
cd src/FloodShield.Shelters.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha-forte>"
dotnet user-secrets set "Jwt:Key" "<texto-aleatorio-com-32+-caracteres>"
```
> Gerar chave: `openssl rand -base64 48` (Linux/Mac/Git Bash).

### 5.3 Variáveis de ambiente (produção / Azure)

| Variável | Descrição |
|---|---|
| `ConnectionStrings__Default` | Connection string do SQL Server / Azure SQL |
| `Jwt__Key` | **Obrigatória em produção** (mín. 32 caracteres) |
| `Jwt__Issuer` / `Jwt__Audience` | Emissor e audiência do token |
| `Seed__AdminEmail` / `Seed__AdminPassword` | Admin inicial (só é criado se a senha for informada) |
| `RateLimiting__PermitLimit` / `RateLimiting__AuthPermitLimit` | Requisições por janela (padrão 60 e 5) |
| `ExternalServices__JavaBackendHealthUrl` | URL do `/health` (ou `/actuator/health`) do backend Java, para o health check |

## 6. Endpoints

| Método | Rota | Acesso | Observação |
|---|---|---|---|
| POST | `/auth/register` | Público | Cria usuário `Citizen` |
| POST | `/auth/login` | Público | Retorna o JWT |
| GET | `/shelters` | Autenticado | Paginação (`page`, `pageSize`) e filtros (`status`, `search`) |
| GET | `/shelters/{id}` | Autenticado | |
| GET | `/shelters/nearby?lat=&lng=&radiusKm=&limit=&onlyAvailable=` | Autenticado | Abrigos mais próximos (Haversine) |
| POST | `/shelters` | Admin | Exige `Idempotency-Key` |
| PUT | `/shelters/{id}` | Admin | |
| DELETE | `/shelters/{id}` | Admin | |
| PATCH | `/shelters/{id}/occupancy` | Admin | Exige `Idempotency-Key`; lotou ⇒ `Full` |
| GET | `/health` | Público | API, banco e backend Java |

**Códigos HTTP usados:** 200, 201, 204, 400 (validação / header ausente), 401, 403, 404, 409 (e-mail duplicado), 422 (regra de negócio / chave de idempotência reutilizada com outro payload), 429 (rate limit), 500.

### Exemplos
```bash
# Login
curl -X POST http://localhost:5080/auth/login -H "Content-Type: application/json" \
  -d '{"email":"admin@floodshield.local","password":"<senha>"}'

# Abrigos próximos (Citizen ou Admin)
curl "http://localhost:5080/shelters/nearby?lat=-23.69&lng=-46.56&radiusKm=10" \
  -H "Authorization: Bearer <TOKEN>"

# Criar abrigo (Admin) — idempotente
curl -X POST http://localhost:5080/shelters \
  -H "Authorization: Bearer <TOKEN>" -H "Content-Type: application/json" \
  -H "Idempotency-Key: 7d1f5c0e-4b7e-4f0b-9c55-0f2a1e9b6a11" \
  -d '{"name":"Ginásio Central","address":"Rua A, 100","latitude":-23.69,"longitude":-46.56,"capacity":150,"phone":"(11) 4000-0000"}'

# Atualizar ocupação (Admin)
curl -X PATCH http://localhost:5080/shelters/1/occupancy \
  -H "Authorization: Bearer <TOKEN>" -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" -d '{"currentOccupancy":150}'
```

## 7. Requisitos da disciplina → onde estão

| Requisito | Implementação |
|---|---|
| **Minimal API RESTful + validações + códigos HTTP + OpenAPI** | `Endpoints/`, `Contracts/` (DataAnnotations), `Program.cs` (Swagger com Bearer, `Produces`/`ProducesProblem` em cada rota) |
| **JWT (autenticação e autorização)** | `TokenService`, `AddJwtBearer` (valida emissor, audiência, expiração e assinatura), política `AdminOnly`; senhas com `PasswordHasher` |
| **Idempotência** | `Infrastructure/IdempotencyFilter.cs`: mesma chave + mesmo payload devolve a resposta salva (header `Idempotent-Replayed`); mesma chave com payload diferente ⇒ 422 |
| **Rate Limiting (429)** | `AddRateLimiter` com janela fixa por IP: política `default` (60/min) e `auth` (5/min); resposta 429 com `Retry-After` |
| **Testes xUnit (unitários + integração)** | `tests/` — ver seção 8 |
| **Health Checks** | `/health`: `api`, `database` (EF Core) e `java-backend` (serviço externo; se estiver fora, status `Degraded`) |

## 8. Testes
```bash
dotnet test
```
- **Unitários:** regras de ocupação/capacidade/status, validação de coordenadas, Haversine, geração do JWT, validação de DTOs.
- **Integração** (`WebApplicationFactory`, EF InMemory): login 200/401, rota protegida sem token 401, Citizen em rota Admin 403, criação 201, payload inválido 400, idempotência (sem duplicar / 422 / 400 sem header), ocupação 422, `nearby` ordenado por distância, rate limit 429, health check.

## 9. Segurança e boas práticas
- Nenhuma credencial no repositório: segredos via *user-secrets* ou variáveis de ambiente; `.env` e `appsettings.Development.json` estão no `.gitignore`.
- Mensagem de login genérica (não revela se o e-mail existe) e rate limit mais rígido em `/auth`.
- Exceções inesperadas retornam 500 sem expor detalhes internos.

## 10. Integração com o restante do FloodShield
- **Mobile (React Native):** consome esta API com o JWT (`Authorization: Bearer`). Telas de abrigos usam `/shelters/nearby`.
- **Backend Java:** o health check da API monitora o `/health` do backend Java (`ExternalServices__JavaBackendHealthUrl`).
- **Cloud (Azure):** há `Dockerfile` pronto; configure as variáveis da seção 5.3 no App Service / Container Apps.
