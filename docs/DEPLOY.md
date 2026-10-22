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

No portal, vá em **Criar recurso → Aplicativo Web** e preencha:

- **Grupo de recursos:** o mesmo do Habit-System.
- **Nome:** por exemplo `arco-davi`, que vira `https://arco-davi.azurewebsites.net`.
- **Publicar:** Código.
- **Pilha de runtime:** .NET 10. É irrelevante, porque o pacote é auto-contido.
- **Sistema operacional:** Windows.
- **Plano:** o plano Free (F1) existente do Habit-System.

### 2. Segredos do app

No Web App, abra **Configurações → Variáveis de ambiente** e adicione:

| Nome | Valor |
|---|---|
| `Auth__PasswordHash` | saída de `dotnet run --project src/Arco.Api -- hash-password "sua-senha"` (sem aspas) |
| `Push__Subject` | `mailto:seu-email@...`: contato enviado aos serviços de push; a Apple recusa `example.com` |

Sem `Auth__PasswordHash` o app não sobe em produção, e o smoke test falha.

### 3. Identidade para o GitHub (OIDC)

1. Crie uma **Identidade Gerenciada Atribuída pelo Usuário**, por exemplo `arco-github`, no mesmo grupo de recursos.
2. Na identidade, abra **Credenciais federadas → Adicionar → GitHub Actions** e preencha:
   - **Organização:** `DaviLuisSantos`
   - **Repositório:** `arco`
   - **Entidade:** Ambiente
   - **Ambiente:** `production`
3. No Web App, abra **Controle de acesso (IAM) → Adicionar atribuição de função** e dê a função
   **Website Contributor** para a identidade `arco-github`.

### 4. GitHub

Os IDs ficam na página **Visão geral** da identidade (Client ID) e da assinatura:

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
  (UptimeRobot, Better Stack etc.) chamando `https://<app>.azurewebsites.net/api/health` a cada 5 min.
  O GitHub Actions não serve para isso: o repositório é privado, e um cron a cada 5 min estoura os minutos gratuitos.
- **60 min de CPU por dia e 1 GB de disco:** sobra para um usuário só.
- **Sem domínio próprio com HTTPS:** use o `*.azurewebsites.net`, que já tem HTTPS. HTTPS é obrigatório
  para o PWA e para o push.

## Backup e logs

- **Banco:** baixe `D:\home\data\arco.db` pelo Kudu, em `https://<app>.scm.azurewebsites.net/DebugConsole`.
  Para uma cópia consistente, pare o app antes ou baixe também os arquivos `-wal`/`-shm`.
- **Logs:** no Web App, **Monitoramento → Fluxo de log**. Para ver erros de inicialização, ative o log
  do sistema de arquivos em **Logs do Serviço de Aplicativo**.
