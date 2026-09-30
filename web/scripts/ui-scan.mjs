// Guards the UI contract (see "## UI" in ../AGENTS.md): design values live in app/app.css as
// tokens, never in a view. Prints `file:line [scope] text` for every hit and exits 1 if any.
// Usage: node scripts/ui-scan.mjs (run from web/, or through `npm run ui:scan`)
//
// Scopes:
// - views (app/routes/**, app/root.tsx, app/components/** outside ui/): literal colours,
//   palette classes, arbitrary px/rem values and any dark: class.
// - primitives (app/components/ui/**): literal colours and palette classes only, so shadcn's
//   token-based dark: refinements in unmodified `shadcn add` output pass.
// - tokens (app/app.css): @custom-variant or a .dark selector. Dark mode follows the OS; a
//   `shadcn add` that rewrites the CSS must not switch it to a class nothing sets.
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve, sep } from "node:path";

const WEB = resolve(import.meta.dirname, "..");
const APP = join(WEB, "app");

const PALETTE =
  "slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose|white|black";
const UTILITIES =
  "bg|text|border(-[xytrbl])?|ring-offset|ring|outline|from|via|to|fill|stroke|shadow|divide|caret|accent|decoration|placeholder";
const COLOUR = `#[0-9a-fA-F]{3,8}\\b|rgba?\\(|hsla?\\(|oklch\\(|\\b(${UTILITIES})-(${PALETTE})\\b`;

// The /10x-ui pattern: colours, arbitrary px/rem values and palette classes, plus dark:.
const VIEW_PATTERN = new RegExp(`${COLOUR}|-\\[[0-9.]+(px|rem)\\]|dark:`);
const PRIMITIVE_PATTERN = new RegExp(COLOUR);
const TOKEN_PATTERN = /@custom-variant|(^|[\s,{])\.dark\b/;

const SOURCE = /\.(tsx?|jsx?)$/;

function walk(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

// Everything under app/components/ui/ is a primitive; every other component, at any depth, is a view.
const UI_DIR = join(APP, "components", "ui");
const isPrimitive = (file) => file.startsWith(UI_DIR + sep);
const components = walk(join(APP, "components")).filter((f) => SOURCE.test(f));
const views = [
  ...walk(join(APP, "routes")).filter((f) => SOURCE.test(f)),
  join(APP, "root.tsx"),
  ...components.filter((f) => !isPrimitive(f)),
];
const primitives = components.filter(isPrimitive);

const scopes = [
  { scope: "views", files: views, pattern: VIEW_PATTERN },
  { scope: "primitives", files: primitives, pattern: PRIMITIVE_PATTERN },
  { scope: "tokens", files: [join(APP, "app.css")], pattern: TOKEN_PATTERN, css: true },
];

// Blanks CSS comments but keeps line numbers, so a comment that mentions .dark is not a selector.
function withoutCssComments(text) {
  return text.replace(/\/\*[\s\S]*?\*\//g, (comment) => comment.replace(/[^\n]/g, " "));
}

let hits = 0;
let files = 0;
for (const { scope, files: list, pattern, css } of scopes) {
  for (const file of list) {
    files += 1;
    const lines = readFileSync(file, "utf8").split("\n");
    const scanned = css ? withoutCssComments(lines.join("\n")).split("\n") : lines;
    scanned.forEach((line, index) => {
      if (!pattern.test(line)) return;
      hits += 1;
      console.log(`${relative(WEB, file)}:${index + 1} [${scope}] ${lines[index].trim()}`);
    });
  }
}

if (hits > 0) {
  console.error(
    `\nui:scan: ${hits} hit(s). Use a token from app/app.css or a component from app/components/ui ` +
      "(see \"## UI\" in AGENTS.md).",
  );
  process.exit(1);
}
console.log(`ui:scan OK: ${files} files, no hardcoded design values.`);
