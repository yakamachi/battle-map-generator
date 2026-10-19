import { cn } from "cn";

// Icon paths from Material Design Icons (https://pictogrammers.com/library/mdi/), Apache License
// 2.0: the hexagon outline of mdi:dice-d20-outline (its baked-in "20" replaced by live <text>
// faces) and mdi:dice-multiple-outline.
const D20_OUTLINE =
  "M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18s-.41-.06-.57-.18l-7.9-4.44A.99.99 0 0 1 3 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18s.41.06.57.18l7.9 4.44c.32.17.53.5.53.88zM12 4.15L5 8.09v7.82l7 3.94l7-3.94V8.09z";
const DICE_MULTIPLE_OUTLINE =
  "M14 8c-.55 0-1-.45-1-1s.45-1 1-1s1 .45 1 1s-.45 1-1 1m-2 4v7H5v-7zm.78-2H4.22C3.55 10 3 10.55 3 11.22v8.56c0 .67.55 1.22 1.22 1.22h8.56c.67 0 1.22-.55 1.22-1.22v-8.56c0-.67-.55-1.22-1.22-1.22m7-7h-8.56C10.55 3 10 3.55 10 4.22V8h2V5h7v7h-3v2h3.78c.67 0 1.22-.55 1.22-1.22V4.22C21 3.55 20.45 3 19.78 3M17 8c-.55 0-1-.45-1-1s.45-1 1-1s1 .45 1 1s-.45 1-1 1m0 3c-.55 0-1-.45-1-1s.45-1 1-1s1 .45 1 1s-.45 1-1 1M7 15c-.55 0-1-.45-1-1s.45-1 1-1s1 .45 1 1s-.45 1-1 1m3 3c-.55 0-1-.45-1-1s.45-1 1-1s1 .45 1 1s-.45 1-1 1";

// The faces shown while rolling, one per quarter-turn hop: a fixed list, so every roll looks the
// same. Each is shown for 1/8 of flick8's 1.8 s, staggered by one hop (0.9 s / 4).
const ROLL_FACES = [14, 3, 19, 8, 12, 1, 17, 6];
const HOP_SECONDS = 0.225;

// SVG geometry for a face number centred in the hexagon (attributes, not design tokens).
const FACE = {
  x: 12,
  y: 15.4,
  textAnchor: "middle",
  fontSize: 9,
  fontWeight: 700,
  fill: "currentColor",
} as const;

type DiceIconProps = {
  // d20: Generate will roll a map for new settings. dice: it will roll a new layout (Reroll).
  variant: "d20" | "dice";
  // The d20 tumbles and its face changes on each hop.
  rolling: boolean;
  // The dice pop in when they mount: only after a Generate, not when Save redraws the map or the
  // form is changed back to the drawn map's settings.
  pop: boolean;
};

// The Generate button's icon (prototype variant D4). Decorative: the button's name stays
// "Generate". Under reduced motion nothing moves and the d20 keeps its resting "20".
export function DiceIcon({ variant, rolling, pop }: DiceIconProps) {
  if (variant === "dice") {
    // Keyed apart from the d20, so it mounts on the swap and, after a Generate, its pop plays.
    return (
      <svg
        key="dice"
        viewBox="0 0 24 24"
        aria-hidden="true"
        className={cn("size-5", pop && "motion-safe:animate-pop")}
      >
        <path fill="currentColor" d={DICE_MULTIPLE_OUTLINE} />
      </svg>
    );
  }

  return (
    <svg
      key="d20"
      viewBox="0 0 24 24"
      aria-hidden="true"
      className={cn("size-5", rolling && "motion-safe:animate-tumble")}
    >
      <path fill="currentColor" d={D20_OUTLINE} />
      {rolling &&
        ROLL_FACES.map((face, i) => (
          <text
            key={face}
            {...FACE}
            className="opacity-0 motion-safe:animate-flick8"
            style={{ animationDelay: `${(-i * HOP_SECONDS).toFixed(3)}s` }}
          >
            {face}
          </text>
        ))}
      <text {...FACE} className={cn(rolling && "motion-safe:opacity-0")}>
        20
      </text>
    </svg>
  );
}
