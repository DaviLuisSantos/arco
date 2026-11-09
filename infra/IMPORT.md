# Importar o ambiente atual para o Terraform

O Azure já tem tudo isto criado à mão (veja `docs/RELATO-AZURE.md`). O Terraform só passa a gerenciar depois do
`import`. **Nada aqui roda no CI**: o `terraform apply` é sempre manual, de uma máquina sua, e só depois de um
`plan` sem destruição.

## 0. Pré-requisitos

- `az login` na conta dona da assinatura.
- Um Storage Account para o state (container `tfstate`), com a função **Storage Blob Data Contributor** para você
  e para a identidade `arco-github` (o job `terraform-plan` do CI lê o state).
- Terraform >= 1.9.

## 1. Configurar

```bash
cd infra
cp backend.hcl.example backend.hcl            # preencha o Storage Account do state
cp terraform.tfvars.example terraform.tfvars  # preencha; os dois arquivos são ignorados pelo git
terraform init -backend-config=backend.hcl
```

No `terraform.tfvars`, preencha com os **valores atuais** do Web App (portal, **Settings → Environment variables**):
`service_plan_name`, `owner_email`, `email_from`, `google_client_id`, `admin_emails`, `stripe_price_id`,
`push_subject` e o que mais existir (`Sentry__Environment` etc. vai em `extra_app_settings`).
O mapa `app_settings` do `azurerm_windows_web_app` é a lista **inteira**: o que não estiver nele é apagado no `apply`.

## 2. Key Vault e segredos

O Terraform cria o cofre e as **referências** `@Microsoft.KeyVault(...)`, nunca o valor dos segredos (iriam para o state).
Os valores vão por fora, com a função **Key Vault Secrets Officer** (já dada a `key_vault_admin_object_ids`):

```bash
terraform output key_vault_secret_names      # um segredo por referência
az keyvault secret set --vault-name <cofre> --name Auth--PasswordHash --file valor.txt   # --file evita o valor no histórico do shell
```

O nome do segredo troca `_` por `-` (`Auth__PasswordHash` vira `Auth--PasswordHash`).
**Só deixe em `key_vault_secret_settings` o que tem segredo gravado**: uma referência sem segredo chega ao app como texto literal
(por exemplo, `Stripe__SecretKey` ligaria a cobrança com um valor inválido). Antes de trocar o valor atual do portal por uma
referência, grave o mesmo valor no cofre e confira no portal se a setting aparece com o selo verde de "Key Vault Reference".

## 3. Importar

IDs de exemplo (substitua `<sub>`). Rode da pasta `infra/`:

```bash
SUB=<subscription id>; RG=arco_group
terraform import azurerm_resource_group.this /subscriptions/$SUB/resourceGroups/$RG
terraform import azurerm_windows_web_app.this /subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.Web/sites/arco
terraform import azurerm_application_insights.this /subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.Insights/components/arco-insights
terraform import azurerm_user_assigned_identity.github /subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.ManagedIdentity/userAssignedIdentities/arco-github
terraform import azurerm_federated_identity_credential.production /subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.ManagedIdentity/userAssignedIdentities/arco-github/federatedIdentityCredentials/arco-production
terraform import azurerm_role_assignment.github_website_contributor <id do role assignment existente>
```

O ID do role assignment (Website Contributor da `arco-github` no Web App) sai de:

```bash
az role assignment list --assignee <client id da arco-github> --scope /subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.Web/sites/arco --query "[].id" -o tsv
```

**Não importe** (o Terraform cria): o Key Vault e seus role assignments, a credencial `arco-pull-request`,
o role `Reader` e os role assignments do Key Vault. A identidade do Web App (`SystemAssigned`) também é criada no primeiro apply.

**O plano F1 não é importado:** ele fica no grupo `HabitSystem`, é compartilhado com outro site e entra só como `data source`.

**No Git Bash**, rode `export MSYS_NO_PATHCONV=1` antes: sem isso, os IDs que começam com `/subscriptions` são corrompidos.

**Se o Web App ainda não tiver identidade**, o import falha com `Invalid index`: troque temporariamente `identity[0].principal_id` por `try(..., null)` no `outputs.tf`. Depois do import, aplique primeiro `-target=azurerm_windows_web_app.this`, para a identidade existir antes do papel que depende dela.

## 4. Conferir o plan (obrigatório antes de qualquer apply)

```bash
terraform plan -out=tfplan
```

Só siga se **todas** estas condições valerem:

1. **Zero `destroy` e zero `replace`** (`-/+`). Um `replace` do Web App ou do plano apaga o app: pare e ajuste as variáveis.
2. **Nenhuma app setting removida.** No diff de `app_settings`, nenhuma linha `-` para uma chave que existe hoje no portal.
   Settings que o Terraform ainda desconhece (`WEBSITE_*` são ignoradas) devem entrar em `terraform.tfvars` (`extra_app_settings`).
3. Os demais diffs são esperados e aditivos: a identidade do Web App, o Key Vault, a credencial de PR e os role assignments.
4. Settings que viram referência ao Key Vault aparecem como alteração de valor, e o segredo correspondente já está no cofre (passo 2).

Só então `terraform apply tfplan`. Se der errado, o `apply` não apaga nada que o plan não mostrou.

## 5. CI

O job `terraform-plan` do `deploy.yml` roda em pull request e só comenta o plano no log (sem `apply`). Ligue-o com:

```bash
gh variable set TF_PLAN_ENABLED --body true
gh variable set TF_STATE_RESOURCE_GROUP --body "<grupo do state>"
gh variable set TF_STATE_STORAGE_ACCOUNT --body "<storage account do state>"
gh variable set TF_STATE_CONTAINER --body tfstate
gh variable set TF_VARS_JSON --body "$(cat ci.tfvars.json)"   # as mesmas variáveis do terraform.tfvars, em JSON
```

`TF_VARS_JSON` não tem segredos (só nomes, IDs e e-mails). Exemplo: `{"subscription_id":"...","service_plan_name":"...","key_vault_name":"...",
"github_oidc_subject_production":"...","github_oidc_subject_pull_request":"..."}`.
O provider do job entra por OIDC com a credencial `arco-pull-request`, criada pelo próprio Terraform: o primeiro `apply` precisa acontecer antes de ligar `TF_PLAN_ENABLED`.
