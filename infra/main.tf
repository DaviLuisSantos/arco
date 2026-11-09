data "azurerm_client_config" "current" {}

locals {
  base_url = "https://${var.custom_hostname}"

  # Configuração que o deploy.yml também grava a cada deploy (mesmos valores).
  fixed_settings = {
    ASPNETCORE_ENVIRONMENT    = "Production"
    "ConnectionStrings__Arco" = "Data Source=D:\\home\\data\\arco.db"
    App__BaseUrl              = local.base_url
  }

  optional_settings = {
    Auth__OwnerEmail       = var.owner_email
    Email__From            = var.email_from
    Auth__Google__ClientId = var.google_client_id
    Admin__Emails          = var.admin_emails
    Stripe__PriceId        = var.stripe_price_id
    Push__Subject          = var.push_subject
  }

  plain_settings = merge(
    local.fixed_settings,
    { for k, v in local.optional_settings : k => v if v != null },
    var.extra_app_settings,
  )

  # Nomes de segredo no Key Vault só aceitam letras, números e hífen: "Auth__X" vira "Auth--X".
  secret_settings = {
    for name in var.key_vault_secret_settings :
    name => "@Microsoft.KeyVault(VaultName=${azurerm_key_vault.this.name};SecretName=${replace(name, "_", "-")})"
  }
}

resource "azurerm_resource_group" "this" {
  name     = var.resource_group_name
  location = var.location
}

# O plano Free fica em outro grupo e é compartilhado com o Habit-System: só leitura, nunca gerenciado aqui.
data "azurerm_service_plan" "this" {
  name                = var.service_plan_name
  resource_group_name = var.service_plan_resource_group_name
}

resource "azurerm_application_insights" "this" {
  name                = var.application_insights_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  application_type    = "web"
  workspace_id        = var.application_insights_workspace_id
}

resource "azurerm_windows_web_app" "this" {
  name                = var.web_app_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  service_plan_id     = data.azurerm_service_plan.this.id
  https_only          = true

  # Mantém o que já vale no portal: o deploy usa OIDC, sem usuário e senha de publicação.
  client_affinity_enabled                        = true
  ftp_publish_basic_authentication_enabled       = false
  webdeploy_publish_basic_authentication_enabled = false

  # Esse mapa é a lista inteira de settings: o que não estiver aqui é removido no apply.
  app_settings = merge(local.plain_settings, local.secret_settings)

  identity {
    type = "SystemAssigned"
  }

  site_config {
    use_32_bit_worker   = true  # o plano Free só roda 32 bits
    always_on           = false # F1 não tem Always On
    http2_enabled       = true
    minimum_tls_version = "1.2"
    ftps_state          = "Disabled"
  }

  # O zip deploy pode acrescentar settings WEBSITE_*; não brigamos com isso.
  # O zip deploy pode acrescentar settings WEBSITE_*; não brigamos com isso.
  lifecycle {
    ignore_changes = [
      app_settings["WEBSITE_RUN_FROM_PACKAGE"],
      app_settings["WEBSITE_ENABLE_SYNC_UPDATE_SITE"],
      app_settings["SCM_DO_BUILD_DURING_DEPLOYMENT"],
    ]
  }
}

resource "azurerm_app_service_custom_hostname_binding" "this" {
  count               = var.manage_custom_hostname_binding ? 1 : 0
  hostname            = var.custom_hostname
  app_service_name    = azurerm_windows_web_app.this.name
  resource_group_name = azurerm_resource_group.this.name
}

# Key Vault com RBAC: a Web App lê segredos pela identidade dela, sem chave.
resource "azurerm_key_vault" "this" {
  name                       = var.key_vault_name
  resource_group_name        = azurerm_resource_group.this.name
  location                   = azurerm_resource_group.this.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
}

# A Web App cria a própria identidade; o role assignment depende dela e as referências
# nas settings dependem só do nome do cofre, então não há ciclo.
resource "azurerm_role_assignment" "web_app_secrets_user" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_windows_web_app.this.identity[0].principal_id
}

resource "azurerm_role_assignment" "key_vault_admins" {
  for_each             = var.key_vault_admin_object_ids
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = each.value
}

# Identidade do GitHub Actions (OIDC), sem senha no GitHub.
resource "azurerm_user_assigned_identity" "github" {
  name                = var.identity_name
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
}

resource "azurerm_federated_identity_credential" "production" {
  name                = "arco-production"
  resource_group_name = azurerm_resource_group.this.name
  parent_id           = azurerm_user_assigned_identity.github.id
  issuer              = "https://token.actions.githubusercontent.com"
  audience            = ["api://AzureADTokenExchange"]
  subject             = var.github_oidc_subject_production
}

# A credencial de production só vale para jobs com environment: production.
# O job terraform-plan roda em pull_request e precisa da própria.
resource "azurerm_federated_identity_credential" "pull_request" {
  name                = "arco-pull-request"
  resource_group_name = azurerm_resource_group.this.name
  parent_id           = azurerm_user_assigned_identity.github.id
  issuer              = "https://token.actions.githubusercontent.com"
  audience            = ["api://AzureADTokenExchange"]
  subject             = var.github_oidc_subject_pull_request
}

# Deploy: escopo só no Web App (menor privilégio).
resource "azurerm_role_assignment" "github_website_contributor" {
  scope                = azurerm_windows_web_app.this.id
  role_definition_name = "Website Contributor"
  principal_id         = azurerm_user_assigned_identity.github.principal_id
}

# terraform plan: leitura do grupo inteiro (o plan só lê, nunca aplica).
resource "azurerm_role_assignment" "github_reader" {
  scope                = azurerm_resource_group.this.id
  role_definition_name = "Reader"
  principal_id         = azurerm_user_assigned_identity.github.principal_id
}
