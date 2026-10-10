#!/usr/bin/env bash
# PostToolUse (Write|Edit): no linter in web/ or api/, so check the edited file with what
# exists — TypeScript diagnostics for web/*.ts(x), `dotnet build` diagnostics for api/*.cs
# and api.Tests/*.cs. Exit 2 + stderr is the only channel Claude sees for this event.
export NO_COLOR=1 FORCE_COLOR=0
command -v jq >/dev/null || { echo "check-edited-file.sh needs jq: install it or port the hook to Node." >&2; exit 2; }

INPUT=$(cat)
FILE=$(printf '%s' "$INPUT" | jq -r '.tool_input.file_path // .tool_input.notebook_path // empty' 2>/dev/null)
CWD=$(printf '%s' "$INPUT" | jq -r '.cwd // empty' 2>/dev/null)
[ -n "$FILE" ] || exit 0
case "$FILE" in /*) ;; *) FILE="${CWD:-$PWD}/$FILE" ;; esac
[ -f "$FILE" ] || exit 0

# Checkout of the edited file, not $CLAUDE_PROJECT_DIR (stale after EnterWorktree, or for a
# sibling worktree neither it nor cwd names). Files of other repositories are skipped.
ROOT=$(git -C "$(dirname "$FILE")" rev-parse --show-toplevel 2>/dev/null) || exit 0
REPO=$(git -C "$ROOT" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)
SESSION_REPO=$(git -C "${CWD:-$PWD}" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)
[ -n "$REPO" ] && [ "$REPO" = "$SESSION_REPO" ] || exit 0

# Register the checkout so the Stop hook sweeps it too, even when it is not the session cwd.
SID=$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2>/dev/null | tr -cd 'A-Za-z0-9_-')
if [ -n "$SID" ] && mkdir -p "${TMPDIR:-/tmp}/claude-hooks"; then
  printf '%s\n' "$ROOT" >>"${TMPDIR:-/tmp}/claude-hooks/$SID.roots"
fi

REL=${FILE#"$ROOT"/}

case "$REL" in
  web/*.ts | web/*.tsx | web/*.mts | web/*.cts | web/*.js | web/*.jsx | web/*.mjs | web/*.cjs)
    command -v npx >/dev/null || { echo "check-edited-file.sh needs npx in web/." >&2; exit 2; }
    WEBREL=${REL#web/}
    cd "$ROOT/web" || exit 2
    npx --no-install react-router typegen >/dev/null 2>&1
    OUTPUT=$(npx --no-install tsc --noEmit --pretty false 2>&1)
    RC=$?
    MINE=$(printf '%s\n' "$OUTPUT" | grep --color=never -F "$WEBREL(")
    if [ -n "$MINE" ]; then
      echo "Type errors in $WEBREL:" >&2
      echo "$MINE" >&2
      exit 2
    fi
    # Failed without any TS diagnostic at all: the typechecker itself did not run.
    if [ "$RC" -ne 0 ] && ! printf '%s\n' "$OUTPUT" | grep -q 'error TS'; then
      echo "Typecheck could not run for $WEBREL:" >&2
      printf '%s\n' "$OUTPUT" | head -n 20 >&2
      exit 2
    fi
    ;;
  api/*.cs | api.Tests/*.cs)
    command -v dotnet >/dev/null || { echo "check-edited-file.sh needs dotnet." >&2; exit 2; }
    cd "$ROOT" || exit 2
    # Builds api.Tests, which project-references api/ — one build covers both directories.
    # dotnet's diagnostics use the absolute path, so FILE (not REL) is the grep anchor.
    OUTPUT=$(dotnet build api.Tests 2>&1)
    RC=$?
    MINE=$(printf '%s\n' "$OUTPUT" | grep --color=never -F "$FILE(")
    if [ -n "$MINE" ]; then
      echo "Build errors in $REL:" >&2
      echo "$MINE" >&2
      exit 2
    fi
    if [ "$RC" -ne 0 ] && ! printf '%s\n' "$OUTPUT" | grep -q 'error CS'; then
      echo "Build could not run for $REL:" >&2
      printf '%s\n' "$OUTPUT" | head -n 20 >&2
      exit 2
    fi
    ;;
  *) exit 0 ;;
esac
exit 0
