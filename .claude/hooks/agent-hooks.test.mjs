// Proves the hook scripts in .claude/hooks/ against sample payloads, as /10x-configure-hook
// requires before handing a hook off: "run every script against a deliberately broken file."
// Runs on a throwaway git repo per case, never on this checkout. The slow real tools
// (`npx tsc`/`vitest`, `dotnet build`/`test`) are stubbed by marker files so the suite stays
// fast and deterministic; git, jq and the hook scripts themselves are real.
//
// Run: node --test .claude/hooks/agent-hooks.test.mjs
import { test, describe, before, after } from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, rmSync, mkdirSync, writeFileSync, chmodSync, cpSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const HOOKS_DIR = dirname(fileURLToPath(import.meta.url));

function sh(cmd, args, opts = {}) {
  const r = spawnSync(cmd, args, { encoding: "utf8", ...opts });
  if (r.status !== 0 && opts.expectSuccess !== false) {
    throw new Error(`${cmd} ${args.join(" ")} failed (${r.status}):\n${r.stdout}\n${r.stderr}`);
  }
  return r;
}

function makeStubBin(dir) {
  mkdirSync(dir, { recursive: true });
  // Fakes react-router typegen / tsc / vitest. A `*.ts`/`*.tsx` file containing
  // HOOK_TEST_BREAK_TS (tsc) or HOOK_TEST_BREAK_VITEST (vitest) fails; otherwise clean.
  writeFileSync(
    join(dir, "npx"),
    `#!/usr/bin/env bash
case "$*" in
  *"react-router typegen"*) exit 0 ;;
esac
case "$*" in
  *"tsc --noEmit"*)
    HIT=0
    while IFS= read -r f; do
      grep -q HOOK_TEST_BREAK_TS "$f" 2>/dev/null && { echo "\${f#./}(1,1): error TS9999: Injected test error."; HIT=1; }
    done < <(find . \\( -name '*.ts' -o -name '*.tsx' \\) 2>/dev/null)
    [ "$HIT" -eq 1 ] && exit 1
    exit 0
    ;;
  *"vitest related"*|*"vitest run"*)
    HIT=0
    while IFS= read -r f; do
      grep -q HOOK_TEST_BREAK_VITEST "$f" 2>/dev/null && { echo "FAIL $f"; HIT=1; }
    done < <(find . \\( -name '*.ts' -o -name '*.tsx' \\) 2>/dev/null)
    [ "$HIT" -eq 1 ] && exit 1
    echo "Tests  0 passed"
    exit 0
    ;;
esac
exit 0
`,
  );
  // Fakes `dotnet build` / `dotnet test`. A `*.cs` file containing HOOK_TEST_BREAK_CS fails
  // the build. HOOK_TEST_DOCKER_FAIL=1 simulates the host-only Testcontainers failure that
  // must be SKIPPED, not blocked; HOOK_TEST_ASSERT_FAIL=1 simulates a real test failure that
  // must still block even though it shares the "dotnet test" code path.
  writeFileSync(
    join(dir, "dotnet"),
    `#!/usr/bin/env bash
case "$1" in
  build)
    HIT=0
    while IFS= read -r f; do
      grep -q HOOK_TEST_BREAK_CS "$f" 2>/dev/null && { echo "$(pwd)/\${f#./}(1,1): error CS9999: Injected test error. [fake.csproj]"; HIT=1; }
    done < <(find . -name '*.cs' 2>/dev/null)
    if [ "$HIT" -eq 1 ]; then echo "Build FAILED."; exit 1; fi
    echo "Build succeeded."
    exit 0
    ;;
  test)
    if [ "\${HOOK_TEST_DOCKER_FAIL:-0}" = "1" ]; then
      echo "Docker.DotNet.DockerContainerNotFoundException: Docker API responded with status code='NotFound'"
      exit 1
    fi
    if [ "\${HOOK_TEST_ASSERT_FAIL:-0}" = "1" ]; then
      echo "Assert.Equal() Failure: Expected: 1, Actual: 2"
      exit 1
    fi
    echo "Passed!"
    exit 0
    ;;
  *) exit 0 ;;
esac
`,
  );
  for (const f of ["npx", "dotnet"]) chmodSync(join(dir, f), 0o755);
}

function initRepo() {
  const root = mkdtempSync(join(tmpdir(), "agent-hooks-"));
  sh("git", ["init", "-q", "-b", "main"], { cwd: root });
  sh("git", ["config", "user.email", "test@example.com"], { cwd: root });
  sh("git", ["config", "user.name", "Hook Test"], { cwd: root });

  mkdirSync(join(root, "web", "app", "map"), { recursive: true });
  mkdirSync(join(root, "api"), { recursive: true });
  mkdirSync(join(root, "api.Tests"), { recursive: true });
  writeFileSync(join(root, "web", "app", "map", "foo.ts"), "export const foo = 1;\n");
  writeFileSync(join(root, "api", "Program.cs"), "// baseline\n");
  writeFileSync(join(root, "api.Tests", "FooTests.cs"), "// baseline\n");
  writeFileSync(join(root, "README.md"), "# fixture repo\n");

  cpSync(HOOKS_DIR, join(root, ".claude", "hooks"), {
    recursive: true,
    filter: (src) => !src.endsWith(".test.mjs"),
  });
  for (const f of ["check-edited-file.sh", "related-tests.sh", "end-of-turn.sh"]) {
    chmodSync(join(root, ".claude", "hooks", f), 0o755);
  }

  sh("git", ["add", "-A"], { cwd: root });
  sh("git", ["commit", "-q", "-m", "baseline"], { cwd: root });
  return root;
}

function runHook(root, script, payload, { env = {} } = {}) {
  const stubDir = join(root, ".bin");
  const r = spawnSync(join(root, ".claude", "hooks", script), [], {
    cwd: root,
    input: typeof payload === "string" ? payload : JSON.stringify(payload),
    encoding: "utf8",
    env: { ...process.env, PATH: `${stubDir}:${process.env.PATH}`, ...env },
  });
  return r;
}

describe("check-edited-file.sh", () => {
  let root;
  before(() => {
    root = initRepo();
    makeStubBin(join(root, ".bin"));
  });
  after(() => rmSync(root, { recursive: true, force: true }));

  test("broken web/*.ts blocks (exit 2, names the file)", () => {
    const file = join(root, "web", "app", "map", "foo.ts");
    writeFileSync(file, "export const foo = 1; // HOOK_TEST_BREAK_TS\n");
    const r = runHook(root, "check-edited-file.sh", {
      tool_name: "Edit",
      cwd: root,
      session_id: "s1",
      tool_input: { file_path: file },
    });
    assert.equal(r.status, 2);
    assert.match(r.stderr, /Type errors in app\/map\/foo\.ts/);
    writeFileSync(file, "export const foo = 1;\n");
  });

  test("clean web/*.ts passes (exit 0)", () => {
    const file = join(root, "web", "app", "map", "foo.ts");
    const r = runHook(root, "check-edited-file.sh", {
      tool_name: "Edit",
      cwd: root,
      session_id: "s1",
      tool_input: { file_path: file },
    });
    assert.equal(r.status, 0);
  });

  test("broken api/*.cs blocks (exit 2, names the file)", () => {
    const file = join(root, "api", "Program.cs");
    writeFileSync(file, "// HOOK_TEST_BREAK_CS\n");
    const r = runHook(root, "check-edited-file.sh", {
      tool_name: "Edit",
      cwd: root,
      session_id: "s1",
      tool_input: { file_path: file },
    });
    assert.equal(r.status, 2);
    assert.match(r.stderr, /Build errors in api\/Program\.cs/);
    writeFileSync(file, "// baseline\n");
  });

  test("clean api/*.cs passes (exit 0)", () => {
    const file = join(root, "api", "Program.cs");
    const r = runHook(root, "check-edited-file.sh", {
      tool_name: "Edit",
      cwd: root,
      session_id: "s1",
      tool_input: { file_path: file },
    });
    assert.equal(r.status, 0);
  });

  test("uncovered file type (README.md) passes (exit 0)", () => {
    const r = runHook(root, "check-edited-file.sh", {
      tool_name: "Edit",
      cwd: root,
      tool_input: { file_path: join(root, "README.md") },
    });
    assert.equal(r.status, 0);
  });

  test("nonexistent file passes (exit 0)", () => {
    const r = runHook(root, "check-edited-file.sh", {
      tool_name: "Edit",
      cwd: root,
      tool_input: { file_path: join(root, "web", "app", "map", "ghost.ts") },
    });
    assert.equal(r.status, 0);
  });

  test("empty payload passes (exit 0)", () => {
    const r = runHook(root, "check-edited-file.sh", {});
    assert.equal(r.status, 0);
  });

  test("unparseable payload passes (exit 0)", () => {
    const r = runHook(root, "check-edited-file.sh", "not json");
    assert.equal(r.status, 0);
  });

  test("no silent success: missing dotnet fails visibly (exit 2), not exit 0", () => {
    // dotnet (and npx) live in /usr/bin next to git/jq/bash/coreutils on this host, so
    // filtering PATH entries can't drop one without the other. Instead, build a minimal
    // bin dir that symlinks only what the script needs besides dotnet/npx, and use that
    // as the entire PATH, so `command -v dotnet` genuinely fails.
    const file = join(root, "api", "Program.cs");
    const minBin = join(root, ".min-bin");
    mkdirSync(minBin, { recursive: true });
    for (const tool of ["bash", "git", "jq", "cat", "dirname", "grep", "tr", "sort", "head", "find", "mkdir", "chmod"]) {
      const real = spawnSync("which", [tool], { encoding: "utf8" }).stdout.trim();
      if (real) sh("ln", ["-sf", real, join(minBin, tool)]);
    }
    const env = { PATH: minBin, TMPDIR: process.env.TMPDIR || "/tmp", HOME: process.env.HOME };
    // dotnet must be absent from this PATH for the assertion below to mean anything.
    assert.equal(spawnSync("dotnet", ["--version"], { env }).error?.code, "ENOENT");

    const r = spawnSync(join(root, ".claude", "hooks", "check-edited-file.sh"), [], {
      cwd: root,
      input: JSON.stringify({ tool_name: "Edit", cwd: root, tool_input: { file_path: file } }),
      encoding: "utf8",
      env,
    });
    assert.equal(r.status, 2);
    assert.match(r.stderr, /needs dotnet/);
  });
});

describe("related-tests.sh (web only)", () => {
  let root;
  before(() => {
    root = initRepo();
    makeStubBin(join(root, ".bin"));
  });
  after(() => rmSync(root, { recursive: true, force: true }));

  test("broken related test blocks (exit 2)", () => {
    const file = join(root, "web", "app", "map", "foo.ts");
    writeFileSync(file, "export const foo = 1; // HOOK_TEST_BREAK_VITEST\n");
    const r = runHook(root, "related-tests.sh", {
      tool_name: "Edit",
      cwd: root,
      tool_input: { file_path: file },
    });
    assert.equal(r.status, 2);
    assert.match(r.stderr, /Tests related to app\/map\/foo\.ts fail/);
    writeFileSync(file, "export const foo = 1;\n");
  });

  test("api/*.cs is not covered per edit (exit 0, no test run)", () => {
    const r = runHook(root, "related-tests.sh", {
      tool_name: "Edit",
      cwd: root,
      tool_input: { file_path: join(root, "api", "Program.cs") },
    });
    assert.equal(r.status, 0);
  });
});

describe("end-of-turn.sh", () => {
  let root;
  before(() => {
    root = initRepo();
    makeStubBin(join(root, ".bin"));
  });
  after(() => rmSync(root, { recursive: true, force: true }));

  test("nothing changed passes (exit 0)", () => {
    const r = runHook(root, "end-of-turn.sh", { cwd: root, stop_hook_active: false });
    assert.equal(r.status, 0);
  });

  test("edit that bypassed the per-edit hook still blocks, names the file", () => {
    const file = join(root, "web", "app", "map", "foo.ts");
    writeFileSync(file, "export const foo = 1; // HOOK_TEST_BREAK_TS\n");
    const r = runHook(root, "end-of-turn.sh", { cwd: root, stop_hook_active: false });
    assert.equal(r.status, 2);
    assert.match(r.stderr, /Typecheck fails \(web/);
    writeFileSync(file, "export const foo = 1;\n");
    sh("git", ["checkout", "--", "web/app/map/foo.ts"], { cwd: root });
  });

  test("retry guard: same broken tree but stop_hook_active=true passes (exit 0)", () => {
    const file = join(root, "web", "app", "map", "foo.ts");
    writeFileSync(file, "export const foo = 1; // HOOK_TEST_BREAK_TS\n");
    const r = runHook(root, "end-of-turn.sh", { cwd: root, stop_hook_active: true });
    assert.equal(r.status, 0);
    writeFileSync(file, "export const foo = 1;\n");
    sh("git", ["checkout", "--", "web/app/map/foo.ts"], { cwd: root });
  });

  test("api build failure blocks (exit 2)", () => {
    const file = join(root, "api", "Program.cs");
    writeFileSync(file, "// HOOK_TEST_BREAK_CS\n");
    const r = runHook(root, "end-of-turn.sh", { cwd: root, stop_hook_active: false });
    assert.equal(r.status, 2);
    assert.match(r.stderr, /api build fails/);
    writeFileSync(file, "// baseline\n");
    sh("git", ["checkout", "--", "api/Program.cs"], { cwd: root });
  });

  test("Docker/Testcontainers host failure is SKIPPED, not blocking (exit 0)", () => {
    writeFileSync(join(root, "api", "Program.cs"), "// touched\n");
    const r = runHook(
      root,
      "end-of-turn.sh",
      { cwd: root, stop_hook_active: false },
      { env: { HOOK_TEST_DOCKER_FAIL: "1" } },
    );
    assert.equal(r.status, 0);
    assert.match(r.stdout, /skipped on this host/i);
    assert.match(r.stdout, /Docker\/Testcontainers/);
    sh("git", ["checkout", "--", "api/Program.cs"], { cwd: root });
  });

  test("a real assertion failure still blocks, even on the dotnet-test path", () => {
    writeFileSync(join(root, "api", "Program.cs"), "// touched\n");
    const r = runHook(
      root,
      "end-of-turn.sh",
      { cwd: root, stop_hook_active: false },
      { env: { HOOK_TEST_ASSERT_FAIL: "1" } },
    );
    assert.equal(r.status, 2);
    assert.match(r.stderr, /api\.Tests fail/);
    sh("git", ["checkout", "--", "api/Program.cs"], { cwd: root });
  });

  test("checkout resolution: a sibling worktree registered by check-edited-file.sh is swept too", () => {
    const worktreeDir = join(root, "..", "agent-hooks-worktree");
    sh("git", ["worktree", "add", "-q", "-b", "wt", worktreeDir], { cwd: root });
    try {
      const wtFile = join(worktreeDir, "web", "app", "map", "foo.ts");
      writeFileSync(wtFile, "export const foo = 1; // HOOK_TEST_BREAK_TS\n");

      // Registers the worktree root, as a real PostToolUse edit there would (session_id
      // shared with the Stop hook call below).
      const reg = runHook(root, "check-edited-file.sh", {
        tool_name: "Edit",
        cwd: root, // session cwd is the main worktree, like a stale $CLAUDE_PROJECT_DIR
        session_id: "sibling-wt",
        tool_input: { file_path: wtFile },
      });
      assert.equal(reg.status, 2); // the sibling-worktree edit itself is broken too

      const r = runHook(root, "end-of-turn.sh", {
        cwd: root,
        session_id: "sibling-wt",
        stop_hook_active: false,
      });
      assert.equal(r.status, 2);
      assert.match(r.stderr, new RegExp(`Typecheck fails \\(web, ${worktreeDir.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\)`));
    } finally {
      rmSync(worktreeDir, { recursive: true, force: true });
      sh("git", ["worktree", "prune"], { cwd: root });
      sh("git", ["branch", "-D", "wt"], { cwd: root });
    }
  });
});
