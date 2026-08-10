# Ambiente de desenvolvimento — Control Plane

> Como montar, do zero, o ambiente local usado para compilar, migrar e testar
> `AppBridge.ControlPlane.*`. Escrito a partir do que foi de fato executado em S010 — não é um plano,
> é o registro do que funcionou.

## 1. .NET 10 SDK

O mirror `apt` da imagem usada nesta sessão não tinha o pacote do .NET 10. Instalado via script
oficial:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
```

O instalador **não** adiciona o SDK ao `PATH` nem define `DOTNET_ROOT` de forma persistente. Em cada
sessão de shell nova (e, no ambiente usado nesta sessão, em cada chamada de comando — o estado de
shell não persiste `export` entre invocações), exporte antes de qualquer `dotnet`:

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

`dotnet-ef` (ferramenta global) também precisa de `DOTNET_ROOT` definido — sem isso, ele falha ao
localizar o runtime mesmo com o SDK no `PATH`.

```bash
dotnet tool install --global dotnet-ef
export PATH="$HOME/.dotnet/tools:$PATH"
```

## 2. PostgreSQL

PostgreSQL 16 local, autenticado por senha (sem Docker/Testcontainers disponíveis neste ambiente —
os testes de integração rodam contra bancos reais, não contêineres efêmeros).

```sql
CREATE ROLE appbridge WITH LOGIN PASSWORD 'appbridge_dev_local_only';
CREATE DATABASE appbridge_dev  OWNER appbridge;
CREATE DATABASE appbridge_test OWNER appbridge;
```

A senha acima é de desenvolvimento local apenas — nunca reaproveitar em ambiente real (RP-06: segredo
nunca em código, documento ou log; isto aqui é a exceção deliberada de um segredo de laboratório sem
valor fora desta máquina, não um precedente).

## 3. Variáveis de ambiente do Control Plane

| Variável | Uso | Valor de desenvolvimento |
|----------|-----|---------------------------|
| `APPBRIDGE_DB_CONNECTION` | `dotnet ef` (design-time) e a aplicação em `dotnet run` | `Host=localhost;Database=appbridge_dev;Username=appbridge;Password=appbridge_dev_local_only` |
| `APPBRIDGE_TEST_DB_CONNECTION` | Suíte `AppBridge.ControlPlane.Infrastructure.Tests` | `Host=localhost;Database=appbridge_test;Username=appbridge;Password=appbridge_dev_local_only` |

## 4. Migrações

```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
export APPBRIDGE_DB_CONNECTION="Host=localhost;Database=appbridge_dev;Username=appbridge;Password=appbridge_dev_local_only"

# gerar uma nova migração
dotnet ef migrations add <Nome> \
  --project src/AppBridge.ControlPlane.Infrastructure \
  --startup-project src/AppBridge.ControlPlane.Infrastructure

# aplicar
dotnet ef database update \
  --project src/AppBridge.ControlPlane.Infrastructure \
  --startup-project src/AppBridge.ControlPlane.Infrastructure

# revisar o SQL antes de confiar numa migração nova
dotnet ef migrations script \
  --project src/AppBridge.ControlPlane.Infrastructure \
  --startup-project src/AppBridge.ControlPlane.Infrastructure
```

## 5. Build e testes

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

dotnet build AppBridge.slnx

export APPBRIDGE_TEST_DB_CONNECTION="Host=localhost;Database=appbridge_test;Username=appbridge;Password=appbridge_dev_local_only"
dotnet test tests/AppBridge.ControlPlane.Infrastructure.Tests
```

`SchemaTests.cs` migra o banco de teste para cima e para baixo dentro de cada `[Fact]` — não é
preciso rodar `dotnet ef database update` manualmente contra `appbridge_test` antes.

## 6. Armadilhas já encontradas

- **Conflito de versão `Microsoft.EntityFrameworkCore.Relational`** (`MSB3277`): em 2026-08-10,
  `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 trazia transitivamente a versão 10.0.4 do pacote
  `Relational`, conflitando com a 10.0.10 referenciada diretamente. Resolvido fixando
  `Microsoft.EntityFrameworkCore.Relational` em 10.0.10 no `.csproj` da Infrastructure — revisitar
  quando o Npgsql publicar uma versão alinhada com o EF Core 10.0.10.
- **`Microsoft.OpenApi` 2.0.0 vulnerável** (GHSA-v5pm-xwqc-g5wc, trazido pelo template
  `dotnet new webapi`): a 3.x quebra o gerador de OpenAPI que acompanha
  `Microsoft.AspNetCore.OpenApi` 10.0.10. Fixado em `2.11.0` (última correção compatível na linha
  2.x) no `.csproj` da Api.
- **`dotnet test` falhando com `Connection refused` em `127.0.0.1:5432`**: o PostgreSQL local não
  sobrevive a um reinício do ambiente/contêiner. Verifique com `sudo service postgresql status` e
  suba com `sudo service postgresql start` antes de rodar qualquer teste de Infraestrutura — não é
  um problema de código, é o serviço parado.
