#!/usr/bin/env bash
# The denylist is a repository secret, so the names never appear in this public repository or in its logs.
set -euo pipefail

if [ -z "${FORBIDDEN_REFERENCES:-}" ]; then
  echo "The FORBIDDEN_REFERENCES secret is not set"
  exit 1
fi

patterns=$(mktemp)
trap 'rm -f "$patterns"' EXIT
printf '%s\n' "$FORBIDDEN_REFERENCES" | sed '/^[[:space:]]*$/d' > "$patterns"

found=0
# Only file names and counts are printed: the matching lines would publish the names.
if git grep -I -i -c -E -f "$patterns" -- .; then
  found=1
fi

history=$(git log --format='%an%n%ae%n%cn%n%ce%n%B' | grep -i -c -E -f "$patterns" || true)
if [ "${history:-0}" -gt 0 ]; then
  echo "commit metadata or messages: $history matching lines"
  found=1
fi

if [ "$found" -eq 1 ]; then
  echo "Forbidden references found"
  exit 1
fi

echo "No forbidden references"
