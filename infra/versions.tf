terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  # Backend parcial: os valores vêm de `backend.hcl` (veja backend.hcl.example)
  # ou de -backend-config no CI. Nenhum segredo entra aqui.
  backend "azurerm" {}
}

provider "azurerm" {
  subscription_id = var.subscription_id

  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }

  # O estado e a API usam o login do Azure AD (az login local, OIDC no CI), sem chaves de conta.
  storage_use_azuread = true
}
