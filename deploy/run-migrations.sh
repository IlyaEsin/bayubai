#!/usr/bin/env bash
# Container Apps jobs start asynchronously, so the deploy waits for the execution to finish and fails with it.
set -euo pipefail

# Under Git Bash on Windows, az CLI output lines end in CRLF; $(...) strips only the LF, so a captured status like "Succeeded\r" would never match the case below.
tsv() { "$@" | tr -d '\r'; }

resource_group="$1"
execution=$(tsv az containerapp job start --name migrations --resource-group "$resource_group" --query name --output tsv)
echo "Started migrations execution $execution"

for _ in $(seq 1 60); do
  # A failed poll (throttling, a network blip) counts as "not finished yet"; the loop's own limit still ends the wait.
  status=$(tsv az containerapp job execution show --name migrations --resource-group "$resource_group" \
    --job-execution-name "$execution" --query properties.status --output tsv) || status="unknown"
  case "$status" in
    Succeeded) echo "Migrations applied"; exit 0 ;;
    Failed | Stopped | Degraded) echo "Migrations execution ended as $status; see its logs in Log Analytics"; exit 1 ;;
  esac
  sleep 10
done

echo "Migrations execution did not finish within 10 minutes"
exit 1
