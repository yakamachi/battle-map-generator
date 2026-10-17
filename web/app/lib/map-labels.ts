import type { BossSize, EncounterType, MapParameters } from "~/api/client";

// The words the UI uses for a map's parameters: the home form's options and the saved-map views
// share them, so a map is described the same way everywhere.
export const ENCOUNTERS: { value: EncounterType; label: string }[] = [
  { value: "skirmish", label: "Skirmish" },
  { value: "boss", label: "Boss fight" },
];

export const BOSS_SIZES: { value: BossSize; label: string }[] = [
  { value: "large", label: "Large" },
  { value: "huge", label: "Huge" },
  { value: "gargantuan", label: "Gargantuan" },
];

// "Skirmish", or "Boss fight (Huge)".
export function encounterLabel({ encounter, bossSize }: MapParameters): string {
  const name = ENCOUNTERS.find((option) => option.value === encounter)?.label ?? encounter;
  if (encounter !== "boss" || !bossSize) return name;
  const size = BOSS_SIZES.find((option) => option.value === bossSize)?.label ?? bossSize;
  return `${name} (${size})`;
}

// "30×20", the map's size in squares.
export function sizeLabel({ width, height }: { width: number; height: number }): string {
  return `${width}×${height}`;
}
