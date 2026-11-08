variable "subscription_id" {
  description = "ID da assinatura do Azure."
  type        = string
}

variable "location" {
  description = "Região de todos os recursos."
  type        = string
  default     = "brazilsouth"
}

variable "resource_group_name" {
  description = "Grupo de recursos existente (importado)."
  type        = string
  default     = "arco_group"
}

variable "service_plan_name" {
  description = "Nome do App Service Plan Free (F1) em uso. Confira no portal: ele veio do Habit-System."
  type        = string
}

variable "web_app_name" {
  description = "Nome do Web App."
  type        = string
  default     = "arco"
}

variable "custom_hostname" {
  description = "Domínio público do app. Ele vira o App__BaseUrl e o Mcp__SelfBaseUrl."
  type        = string
  default     = "arco.daviluis.dev"
}

variable "manage_custom_hostname_binding" {
  description = "Cria o binding do domínio no Web App. Deixe false enquanto o domínio for só um proxy (Cloudflare) na frente do *.azurewebsites.net."
  type        = bool
  default     = false
}

variable "identity_name" {
  description = "Managed identity usada pelo GitHub Actions."
  type        = string
  default     = "arco-github"
}

variable "github_oidc_subject_production" {
  description = "Subject do token OIDC do GitHub para o environment production. Com subject imutável: repo:<org>@<orgId>/<repo>@<repoId>:environment:production (veja docs/RELATO-AZURE.md)."
  type        = string
}

variable "github_oidc_subject_pull_request" {
  description = "Subject do token OIDC do GitHub para pull requests, no mesmo formato do de production: repo:<org>@<orgId>/<repo>@<repoId>:pull_request."
  type        = string
}

variable "key_vault_name" {
  description = "Nome do Key Vault (3 a 24 caracteres, único no Azure)."
  type        = string
}

variable "key_vault_admin_object_ids" {
  description = "Object IDs (usuários ou grupos) que gravam os valores dos segredos no Key Vault (função Key Vault Secrets Officer)."
  type        = set(string)
  default     = []
}

variable "key_vault_secret_settings" {
  description = <<-EOT
    App settings cujo valor é uma referência ao Key Vault. O Terraform só cria a referência:
    o valor do segredo é gravado fora dele (veja IMPORT.md), para não ir ao state.
    Remova da lista o que ainda não tem segredo no cofre, senão o app recebe o texto da referência como valor.
  EOT
  type        = set(string)
  default = [
    "Auth__PasswordHash",
    "Email__ResendApiKey",
    "Stripe__SecretKey",
    "Stripe__WebhookSecret",
    "Auth__Google__ClientSecret",
    "Push__VapidPrivateKey",
    "Sentry__Dsn",
    "APPLICATIONINSIGHTS_CONNECTION_STRING",
    "ServiceBus__ConnectionString",
  ]
}

# App settings que não são segredo. Valor null = a setting não existe hoje e não é criada.

variable "owner_email" {
  description = "Auth__OwnerEmail."
  type        = string
  default     = null
}

variable "email_from" {
  description = "Email__From."
  type        = string
  default     = null
}

variable "google_client_id" {
  description = "Auth__Google__ClientId."
  type        = string
  default     = null
}

variable "admin_emails" {
  description = "Admin__Emails (separados por vírgula)."
  type        = string
  default     = null
}

variable "stripe_price_id" {
  description = "Stripe__PriceId."
  type        = string
  default     = null
}

variable "push_subject" {
  description = "Push__Subject (mailto: de contato para os serviços de push)."
  type        = string
  default     = null
}

variable "extra_app_settings" {
  description = "Outras settings que não são segredo (ex.: Sentry__Environment, Sentry__TracesSampleRate)."
  type        = map(string)
  default     = {}
}
