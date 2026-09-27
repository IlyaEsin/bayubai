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
# Only file names and counts are printed: the matching lines would publish the names; stderr is dropped because git and grep quote a bad pattern there.
if git grep -I -i -c -E -f "$patterns" -- . 2>/dev/null; then
  found=1
else
  status=$?
  if [ "$status" -ne 1 ]; then
    echo "git grep failed (exit $status); check that every FORBIDDEN_REFERENCES line is a valid extended regex"
    exit 2
  fi
fi

if history=$(git log --format='%an%n%ae%n%cn%n%ce%n%B' | grep -i -c -E -f "$patterns" 2>/dev/null); then
  echo "commit metadata or messages: $history matching lines"
  found=1
else
  status=$?
  if [ "$status" -ne 1 ]; then
    echo "grep over the history failed (exit $status); check that every FORBIDDEN_REFERENCES line is a valid extended regex"
    exit 2
  fi
fi

if [ "$found" -eq 1 ]; then
  echo "Forbidden references found"
  exit 1
fi

echo "No forbidden references"
