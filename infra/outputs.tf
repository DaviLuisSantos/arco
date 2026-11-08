output "web_app_default_hostname" {
  description = "Endereço *.azurewebsites.net do Web App."
  value       = azurerm_windows_web_app.this.default_hostname
}

output "web_app_principal_id" {
  description = "Identidade gerenciada da Web App (lê o Key Vault)."
  value       = azurerm_windows_web_app.this.identity[0].principal_id
}

output "key_vault_uri" {
  description = "URI do Key Vault, onde ficam os valores dos segredos."
  value       = azurerm_key_vault.this.vault_uri
}

output "key_vault_secret_names" {
  description = "Nomes dos segredos que o Key Vault precisa ter, um por referência nas app settings."
  value       = sort([for name in var.key_vault_secret_settings : replace(name, "_", "-")])
}

output "github_identity_client_id" {
  description = "Vai para o secret AZURE_CLIENT_ID do GitHub."
  value       = azurerm_user_assigned_identity.github.client_id
}

output "tenant_id" {
  description = "Vai para o secret AZURE_TENANT_ID do GitHub."
  value       = data.azurerm_client_config.current.tenant_id
}
