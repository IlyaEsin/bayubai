#!/usr/bin/env bash
# One-time Azure and GitHub setup before the first deploy, run by the owner signed in to `az login` and `gh auth login`.
# Safe to re-run: every step checks what already exists; secrets are typed at the prompt and never echoed.
set -euo pipefail

# Under Git Bash on Windows, az/gh CLI output lines end in CRLF; $(...) strips only the LF, leaving a trailing \r that breaks GUIDs, scopes and stored GitHub variables.
tsv() { "$@" | tr -d '\r'; }

subscription="${1:?usage: bootstrap.sh <subscription-id> <domain> <budget-email>}"
domain="${2:?usage: bootstrap.sh <subscription-id> <domain> <budget-email>}"
budget_email="${3:?usage: bootstrap.sh <subscription-id> <domain> <budget-email>}"

repo="IlyaEsin/bayubai"
location="westeurope"
resource_group="rg-bayubai"
# Key Vault names are global; the suffix keeps the name stable for this subscription.
vault="kv-bayubai-$(printf '%s' "$subscription" | sha256sum | cut -c1-6)"
deploy_app="bayubai-deploy"

az account set --subscription "$subscription"
tenant=$(tsv az account show --query tenantId --output tsv)

echo "== Resource providers"
for namespace in Microsoft.App Microsoft.ContainerRegistry Microsoft.DBforPostgreSQL Microsoft.KeyVault \
  Microsoft.ManagedIdentity Microsoft.OperationalInsights Microsoft.Insights Microsoft.Web Microsoft.Consumption; do
  az provider register --namespace "$namespace" --wait
done

echo "== Resource group and Key Vault"
az group create --name "$resource_group" --location "$location" --output none
if ! az keyvault show --name "$vault" --output none 2>/dev/null; then
  az keyvault create --name "$vault" --resource-group "$resource_group" --location "$location" \
    --enable-rbac-authorization true --output none
fi
vault_id=$(tsv az keyvault show --name "$vault" --query id --output tsv)
group_id=$(tsv az group show --name "$resource_group" --query id --output tsv)

me=$(tsv az ad signed-in-user show --query id --output tsv)
az role assignment create --assignee-object-id "$me" --assignee-principal-type User \
  --role "Key Vault Secrets Officer" --scope "$vault_id" --output none

# A new role assignment can take a few minutes to apply, so the first secret write is retried.
set_secret() {
  local name="$1" value="$2" error=""
  for _ in $(seq 1 30); do
    # The last error is kept so a lasting permission or network failure is visible.
    if error=$(az keyvault secret set --vault-name "$vault" --name "$name" --value "$value" --output none 2>&1); then
      return 0
    fi
    sleep 10
  done
  echo "Could not write secret $name: $error"
  return 1
}

ask_secret() {
  local name="$1" prompt="$2" value
  if az keyvault secret show --vault-name "$vault" --name "$name" --output none 2>/dev/null; then
    echo "Secret $name already set"
    return 0
  fi
  for _ in 1 2 3; do
    read -r -s -p "$prompt: " value
    echo
    if [ -n "$value" ]; then
      set_secret "$name" "$value"
      return
    fi
    echo "The value cannot be empty"
  done
  echo "No value entered for $name"
  return 1
}

echo "== Secrets"
# The server admin is used only by the migrations job; the API signs in as a role the job creates with the second password.
for name in postgres-password postgres-app-password; do
  if ! az keyvault secret show --vault-name "$vault" --name "$name" --output none 2>/dev/null; then
    set_secret "$name" "$(openssl rand -base64 36 | tr -d '/+=')"
  fi
done
ask_secret admin-email "Admin email (the address you sign in with)"
ask_secret email-username "Brevo SMTP login"
ask_secret email-password "Brevo SMTP key"

echo "== Budget"
az deployment group create --resource-group "$resource_group" --template-file deploy/budget.bicep \
  --parameters startDate="$(date -u +%Y-%m-01)T00:00:00Z" email="$budget_email" --output none

echo "== Deploy identity for GitHub Actions (OIDC, no stored credentials)"
app_id=$(tsv az ad app list --display-name "$deploy_app" --query "[0].appId" --output tsv)
if [ -z "$app_id" ]; then
  app_id=$(tsv az ad app create --display-name "$deploy_app" --query appId --output tsv)
fi
if ! az ad sp show --id "$app_id" --output none 2>/dev/null; then
  az ad sp create --id "$app_id" --output none
fi
sp_id=$(tsv az ad sp show --id "$app_id" --query id --output tsv)
if [ -z "$(tsv az ad app federated-credential list --id "$app_id" --query "[?name=='github-production'].name" --output tsv)" ]; then
  az ad app federated-credential create --id "$app_id" --parameters "{
    \"name\": \"github-production\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$repo:environment:production\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }" --output none
fi
# The Aspire template is subscription-scoped (it declares the resource group) and assigns roles to the app identities.
az role assignment create --assignee-object-id "$sp_id" --assignee-principal-type ServicePrincipal \
  --role Contributor --scope "/subscriptions/$subscription" --output none
az role assignment create --assignee-object-id "$sp_id" --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" --scope "$group_id" --output none
az role assignment create --assignee-object-id "$sp_id" --assignee-principal-type ServicePrincipal \
  --role "Key Vault Secrets User" --scope "$vault_id" --output none

echo "== GitHub environment and variables"
gh api --method PUT "repos/$repo/environments/production" \
  -F "deployment_branch_policy[protected_branches]=false" \
  -F "deployment_branch_policy[custom_branch_policies]=true" > /dev/null
if [ -z "$(tsv gh api "repos/$repo/environments/production/deployment-branch-policies" --jq '.branch_policies[] | select(.name=="main") | .name')" ]; then
  gh api --method POST "repos/$repo/environments/production/deployment-branch-policies" -f name=main -f type=branch > /dev/null
fi
gh variable set AZURE_CLIENT_ID --env production --repo "$repo" --body "$app_id"
gh variable set AZURE_TENANT_ID --env production --repo "$repo" --body "$tenant"
gh variable set AZURE_SUBSCRIPTION_ID --env production --repo "$repo" --body "$subscription"
gh variable set AZURE_RESOURCE_GROUP --env production --repo "$repo" --body "$resource_group"
gh variable set DEPLOY_DOMAIN --env production --repo "$repo" --body "$domain"
gh variable set DEPLOY_KEY_VAULT --env production --repo "$repo" --body "$vault"

echo "Done. Key Vault: $vault, resource group: $resource_group"
