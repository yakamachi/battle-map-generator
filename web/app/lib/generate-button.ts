import type { BossSize, EncounterType, MapParameters } from "~/api/client";

// The Generate button's resting look. Error is not one: after a failure no map is drawn, so the
// button is back to "ready", and the shake that marks the failure is a one-shot animation.
export type GenerateButtonState = "ready" | "rolling" | "reroll";

export type GenerateButtonInput = {
  // A map is being generated or drawn (home's busy).
  busy: boolean;
  // The drawn map's parameters, or null when no map is drawn.
  drawnParameters: MapParameters | null;
  // The form's values. bossSize keeps its value while a skirmish hides the field.
  form: { roomCount: number; encounter: EncounterType; bossSize: BossSize };
};

// "reroll" when Generate would roll a new layout with the drawn map's settings; "ready" when there
// is no map yet or the form has changed since. Boss size counts only on a boss fight, as in the
// request home sends (a skirmish map echoes bossSize: null).
export function generateButtonState({
  busy,
  drawnParameters,
  form,
}: GenerateButtonInput): GenerateButtonState {
  if (busy) return "rolling";
  if (!drawnParameters) return "ready";
  const same =
    drawnParameters.roomCount === form.roomCount &&
    drawnParameters.encounter === form.encounter &&
    (form.encounter !== "boss" || drawnParameters.bossSize === form.bossSize);
  return same ? "reroll" : "ready";
}
