#!/usr/bin/env bash
# Stop: sweep everything this turn changed before the agent finishes, one retry.
# web/: whole-suite typecheck + tests (both ~2s, verified). api/ + api.Tests/: a build pass
# (type/syntax errors) then the full xUnit suite (~37s, needs Docker/Testcontainers for the
# SQL Server fixture). A Testcontainers/Docker failure is a host problem, not application
# code — it is reported as SKIPPED, never silently dropped and never blocked on.
export NO_COLOR=1 FORCE_COLOR=0
command -v jq >/dev/null || { echo "end-of-turn.sh needs jq: install it or port the hook to Node." >&2; exit 2; }

INPUT=$(cat)

# Already sent back once by this hook: let it finish. The commit gate catches the rest.
ACTIVE=$(printf '%s' "$INPUT" | jq -r '.stop_hook_active // false' 2>/dev/null)
LOOPS=$(printf '%s' "$INPUT" | jq -r '.loop_count // 0' 2>/dev/null)
if [ "$ACTIVE" = "true" ] || [ "${LOOPS:-0}" != "0" ]; then
  exit 0
fi

# Checkouts to sweep: the session's (payload cwd follows EnterWorktree, $CLAUDE_PROJECT_DIR
# does not) plus every checkout check-edited-file.sh registered for this session.
CWD=$(printf '%s' "$INPUT" | jq -r '.cwd // empty' 2>/dev/null)
SID=$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2>/dev/null | tr -cd 'A-Za-z0-9_-')
REGISTRY="${TMPDIR:-/tmp}/claude-hooks/${SID:-none}.roots"
ROOTS=()
while IFS= read -r r; do
  [ -n "$r" ] && [ -d "$r" ] && ROOTS+=("$r")
done <<EOF
$({ git -C "${CWD:-$PWD}" rev-parse --show-toplevel 2>/dev/null; [ -n "$SID" ] && cat "$REGISTRY" 2>/dev/null; } | sort -u)
EOF
if [ "${#ROOTS[@]}" -eq 0 ]; then
  jq -cn --arg m "end-of-turn.sh: no git checkout at ${CWD:-$PWD}, nothing checked." '{systemMessage: $m}'
  exit 0
fi

SKIPPED=""
REPORT=""

for ROOT in "${ROOTS[@]}"; do
  cd "$ROOT" || continue

  # Changed and new files. Nothing changed (a Q&A turn): nothing to check here. This also
  # catches files rewritten through a shell command, which never reach a per-edit hook.
  CHANGED=$({ git diff --name-only HEAD; git ls-files -o --exclude-standard; } 2>/dev/null | sort -u)
  [ -n "$CHANGED" ] || continue

  WEB_TOUCHED=0
  API_TOUCHED=0
  while IFS= read -r f; do
    case "$f" in
      web/*.ts | web/*.tsx | web/*.mts | web/*.cts | web/*.js | web/*.jsx | web/*.mjs | web/*.cjs)
        [ -f "$f" ] && WEB_TOUCHED=1
        ;;
      api/*.cs | api.Tests/*.cs)
        [ -f "$f" ] && API_TOUCHED=1
        ;;
    esac
  done <<EOF
$CHANGED
EOF

  if [ "$WEB_TOUCHED" -eq 1 ]; then
    command -v npx >/dev/null || { REPORT="$REPORT
web/ check needs npx, not found ($ROOT).
"; continue; }
    if ! OUT=$(cd "$ROOT/web" && { npx --no-install react-router typegen >/dev/null 2>&1; npx --no-install tsc --noEmit --pretty false; } 2>&1 </dev/null); then
      REPORT="$REPORT
Typecheck fails (web, $ROOT):
$OUT
"
    fi
    if ! OUT=$(cd "$ROOT/web" && npx --no-install vitest run 2>&1 </dev/null); then
      REPORT="$REPORT
web tests fail ($ROOT):
$OUT
"
    fi
  fi

  if [ "$API_TOUCHED" -eq 1 ]; then
    command -v dotnet >/dev/null || { REPORT="$REPORT
api/ check needs dotnet, not found ($ROOT).
"; continue; }
    BUILD_OUT=$(cd "$ROOT" && dotnet build api.Tests 2>&1 </dev/null)
    BUILD_RC=$?
    if [ "$BUILD_RC" -ne 0 ]; then
      REPORT="$REPORT
api build fails ($ROOT):
$BUILD_OUT
"
    else
      TEST_OUT=$(cd "$ROOT" && dotnet test api.Tests --no-build 2>&1 </dev/null)
      TEST_RC=$?
      if [ "$TEST_RC" -ne 0 ]; then
        # Host-only failure (Docker down, Testcontainers can't reach the daemon) vs. a real
        # assertion failure: a Docker/Testcontainers exception with no Assert failure text
        # alongside it is the host, not the code. Never let this narrow the gate silently.
        if printf '%s' "$TEST_OUT" | grep -qE 'DockerContainerNotFoundException|TestcontainersException|Cannot connect to the Docker daemon|Docker API responded with status code' \
          && ! printf '%s' "$TEST_OUT" | grep -q 'Assert\.'; then
          SKIPPED="$SKIPPED api.Tests ($ROOT: Docker/Testcontainers error on this host, not application code);"
        else
          REPORT="$REPORT
api.Tests fail ($ROOT):
$TEST_OUT
"
        fi
      fi
    fi
  fi
done

if [ -n "$REPORT" ]; then
  [ -n "$SKIPPED" ] && REPORT="$REPORT
Skipped on this host:$SKIPPED
"
  echo "Fix these before you finish:$REPORT" >&2
  exit 2
fi
# Green: the registered checkouts are clean, start the next turn with an empty registry.
[ -n "$SID" ] && : >"$REGISTRY" 2>/dev/null
[ -n "$SKIPPED" ] && jq -cn --arg m "end-of-turn.sh passed, but skipped on this host:$SKIPPED" '{systemMessage: $m}'
exit 0
