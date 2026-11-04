# Deploy no Azure App Service (Windows, plano Free)

A esteira `.github/workflows/deploy.yml` roda em todo PR (lint e build do web, testes e publish da API)
e, a cada push na `main`, publica no App Service:

1. Builda o web e copia `web/dist` para o `wwwroot` da API.
2. Publica a API **auto-contida para `win-x86`**. O plano Free do Windows só roda processos de 32 bits,
   e assim o app não depende do runtime .NET instalado no App Service.
3. Faz login no Azure por OIDC, sem senha guardada no GitHub, e grava a configuração que não é segredo:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ConnectionStrings__Arco=Data Source=D:\home\data\arco.db`
   - worker de 32 bits, HTTP/2, TLS 1.2 e FTP desligado
4. Faz o deploy por zip e um smoke test em `/api/health`.

O banco, as chaves dos cookies (`keys/`) e as chaves de push (`vapid.json`) ficam em `D:\home\data`.
Essa pasta é persistente e fica fora do pacote, então sobrevive a deploys e reinícios.

## Configuração única

### 1. Criar o Web App

No portal, vá em **Create a resource → Web App** e preencha:

- **Resource Group:** o mesmo do Habit-System.
- **Name:** por exemplo `arco-davi`. Se o portal acrescentar um sufixo ao endereço (`arco-davi-abc123.westus3-01.azurewebsites.net`), tudo bem: a esteira lê o endereço real do Azure.
- **Publish:** Code.
- **Runtime stack:** .NET 10. É irrelevante, porque o pacote é auto-contido.
- **Operating System:** Windows.
- **Region:** a mesma do plano do Habit-System.
- **Windows Plan:** o plano Free (F1) existente do Habit-System. Confira se **Pricing plan** mostra **Free F1**, não Premium.
- Aba **Deployment:** deixe a implantação contínua desativada; a esteira é a deste repositório.
- Aba **Monitoring:** **Enable Application Insights = No**.

### 2. Segredos do app

No Web App, abra **Settings → Environment variables → App settings → + Add**, adicione as duas e clique em **Apply**:

| Nome | Valor |
|---|---|
| `Auth__PasswordHash` | saída de `dotnet run --project src/Arco.Api -- hash-password "sua-senha"` (sem aspas) |
| `Push__Subject` | `mailto:seu-email@...`: contato enviado aos serviços de push; a Apple recusa `example.com` |

Sem `Auth__PasswordHash` o app não sobe em produção, e o smoke test falha.

### 3. Identidade para o GitHub (OIDC)

1. Na busca do topo, abra **Managed Identities → Create** e crie a identidade (por exemplo `arco-github`) no mesmo grupo de recursos e região.
2. Na identidade, abra **Settings → Federated credentials → + Add Credential**, escolha o cenário
   **GitHub Actions deploying Azure resources** e preencha:
   - **Organization:** `DaviLuisSantos`
   - **Repository:** `arco`
   - **Entity:** Environment
   - **Environment:** `production`
   - **Name:** `arco-production`
3. No Web App, abra **Access control (IAM) → + Add → Add role assignment** e dê a função
   **Website Contributor** para a identidade: em **Members**, escolha **Managed identity** e
   selecione `arco-github`. Termine em **Review + assign**.

### 4. GitHub

O **Client ID** fica no **Overview** da identidade. O **Tenant ID** e o **Subscription ID** também aparecem lá:

```bash
gh secret set AZURE_CLIENT_ID --body "<client id da identidade>"
gh secret set AZURE_TENANT_ID --body "<tenant id>"
gh secret set AZURE_SUBSCRIPTION_ID --body "<subscription id>"
gh variable set AZURE_WEBAPP_NAME --body "arco-davi"
gh variable set AZURE_RESOURCE_GROUP --body "<grupo de recursos>"
```

O ambiente `production` é criado no primeiro deploy. Ele também pode ser criado antes, em
**Settings → Environments**, para exigir aprovação.

Depois disso, rode o workflow em **Actions → CI / Deploy Azure → Run workflow**, ou faça um push na `main`.

## Limites do plano Free

- **Sem Always On:** o app hiberna após cerca de 20 min sem requisições, e com ele o loop que envia
  os lembretes push. Para os lembretes chegarem na hora, cadastre um monitor externo gratuito
  (UptimeRobot, Better Stack etc.) chamando `https://arco.daviluis.dev/api/health` a cada 5 min.
  O GitHub Actions não serve para isso: o repositório é privado, e um cron a cada 5 min estoura os minutos gratuitos.
- **60 min de CPU por dia e 1 GB de disco:** sobra para um usuário só.
- **Domínio:** o endereço público é `https://arco.daviluis.dev`, um proxy na frente do `*.azurewebsites.net`
  (que continua funcionando e já tem HTTPS). HTTPS é obrigatório para o PWA e para o push. Use sempre o domínio
  próprio no app instalado, nas chaves de API e no MCP: as inscrições de push e o PWA ficam presos à origem.

## Backup e logs

- **Automático:** o próprio app grava uma cópia consistente por dia em `D:\home\data\backups\arco-AAAA-MM-DD.db`
  e guarda os últimos 14 dias. A cópia sai na primeira hora em que o app está acordado no dia; com o monitor do
  `/api/health` ligado, sai logo depois da meia-noite. O Perfil mostra a data da última cópia.
- **No seu aparelho:** Perfil → **Baixar backup** baixa uma cópia na hora (`GET /api/backup`). Faça de vez em
  quando: é a única cópia fora do Azure.
- **Restaurar:** pare o Web App; pelo Kudu (**Development Tools → Advanced Tools → Debug console**), substitua
  `D:\home\data\arco.db` pelo backup e apague `arco.db-wal` e `arco.db-shm` se existirem; inicie o Web App.
- **Logs:** no Web App, **Monitoring → Log stream**. Para ver erros de inicialização, ative
  **Application logging (Filesystem)** em **Monitoring → App Service logs**.

## Servidor MCP

As ferramentas do MCP (`/api/mcp`) chamam a própria API pelo endereço público da requisição. Se algum dia isso
falhar no ambiente (ex.: o app não alcança o próprio domínio), defina `Mcp__SelfBaseUrl` nas configurações do Web App
com a URL base da API (ex.: `https://arco.daviluis.dev`).
