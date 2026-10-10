#!/usr/bin/env bash
# PostToolUse (Write|Edit): run the web test suite for an edited web/ file. `vitest related`
# runs the whole suite in this repo regardless (verified: only 4 test files, ~2s) — still far
# cheaper than running api.Tests per edit (Testcontainers/SQL Server; see end-of-turn.sh).
# api/*.cs and api.Tests/*.cs have no per-edit test check for the same reason.
export NO_COLOR=1 FORCE_COLOR=0
command -v jq >/dev/null || { echo "related-tests.sh needs jq: install it or port the hook to Node." >&2; exit 2; }

INPUT=$(cat)
FILE=$(printf '%s' "$INPUT" | jq -r '.tool_input.file_path // .tool_input.notebook_path // empty' 2>/dev/null)
CWD=$(printf '%s' "$INPUT" | jq -r '.cwd // empty' 2>/dev/null)
[ -n "$FILE" ] || exit 0
case "$FILE" in /*) ;; *) FILE="${CWD:-$PWD}/$FILE" ;; esac
[ -f "$FILE" ] || exit 0

ROOT=$(git -C "$(dirname "$FILE")" rev-parse --show-toplevel 2>/dev/null) || exit 0
REPO=$(git -C "$ROOT" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)
SESSION_REPO=$(git -C "${CWD:-$PWD}" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)
[ -n "$REPO" ] && [ "$REPO" = "$SESSION_REPO" ] || exit 0

REL=${FILE#"$ROOT"/}
case "$REL" in
  web/*.ts | web/*.tsx | web/*.mts | web/*.cts | web/*.js | web/*.jsx | web/*.mjs | web/*.cjs) ;;
  *) exit 0 ;;
esac
WEBREL=${REL#web/}

command -v npx >/dev/null || { echo "related-tests.sh needs npx in web/." >&2; exit 2; }
cd "$ROOT/web" || exit 2

if ! OUTPUT=$(npx --no-install vitest related "$WEBREL" --run 2>&1); then
  echo "Tests related to $WEBREL fail:" >&2
  echo "$OUTPUT" >&2
  exit 2
fi
exit 0
