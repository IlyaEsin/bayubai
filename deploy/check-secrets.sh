#!/usr/bin/env bash
# A Key Vault reference to a missing secret fails the new revision late and quietly, so the deploy checks every one first.
set -euo pipefail

vault="$1"
# The connection string secrets are written by the deploy itself.
names=$(grep -h -A1 "Microsoft.KeyVault/vaults/secrets@[^']*' existing" infra/*/*.bicep \
  | sed -n "s/^  name: '\(.*\)'$/\1/p" | grep -v '^connectionstrings--' | sort -u)

missing=0
for name in $names; do
  if ! az keyvault secret show --vault-name "$vault" --name "$name" --output none 2>/dev/null; then
    echo "Missing Key Vault secret: $name"
    missing=1
  fi
done

if [ "$missing" -eq 0 ]; then
  echo "All referenced secrets exist: $(echo $names | tr ' ' ',')"
fi
exit "$missing"
