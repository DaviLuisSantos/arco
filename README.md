# Arco

App pessoal de hábitos com **progresso e objetivo**. Os check-ins diários alimentam hábitos com níveis, os hábitos servem a objetivos com prazo, e os objetivos vivem em ciclos de 6 semanas. Tudo isso gera XP e nível de jogador. Conceito completo em [docs/PRODUCT.md](docs/PRODUCT.md); contrato da API em [docs/API.md](docs/API.md).

- **Backend:** .NET 10 (Minimal APIs, EF Core 10, SQLite) em `src/Arco.Api`, com testes em `tests/Arco.Api.Tests`.
- **Frontend:** React 19 + Vite + TypeScript + TanStack Query + Tailwind v4 + PWA, em `web/`.
- **Produção:** um único processo. O ASP.NET serve a API (`/api/*`) e a SPA (`wwwroot`).

## Desenvolvimento

```bash
dotnet run --project src/Arco.Api   # API em http://localhost:5080 (senha "arco" em Development)
cd web && npm install && npm run dev  # http://localhost:5173 (proxy /api → :5080)
```

```bash
dotnet test                          # testes do backend
cd web && npm run lint && npm run build
```

Para criar uma migration:

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Nome> --project src/Arco.Api --output-dir Infrastructure/Migrations
```

As migrations são aplicadas na inicialização. Se uma falhar, o app não sobe.

## Deploy no Azure (App Service Free)

A esteira do GitHub Actions publica a cada push na `main`. A configuração única (Web App,
identidade OIDC, segredos) e os limites do plano Free estão em [docs/DEPLOY.md](docs/DEPLOY.md).

## Deploy (um servidor)

Requisitos: uma VPS com Docker e um domínio apontando para ela. O HTTPS é obrigatório para o PWA e para as notificações push.

1. Gere o hash da sua senha: `dotnet run --project src/Arco.Api -- hash-password "sua-senha"`.
2. Copie `.env.example` para `.env` e preencha `ARCO_DOMAIN` e `ARCO_PASSWORD_HASH` (entre aspas simples).
3. Rode `docker compose up -d --build`.

O Caddy emite o certificado automaticamente. O banco e as chaves dos cookies ficam no volume `arco-data` (`/data`).

**Backup:** o app guarda sozinho uma cópia por dia em `/data/backups` (últimos 14 dias), e o Perfil tem o botão
"Baixar backup". Para copiar o banco do volume à mão:

```bash
docker compose cp arco:/data/arco.db ./backup.db
```
