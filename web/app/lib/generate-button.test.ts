import { describe, expect, test } from "vitest";
import type { MapParameters } from "~/api/client";
import { generateButtonState, type GenerateButtonInput } from "./generate-button";

const SKIRMISH: MapParameters = { roomCount: 6, encounter: "skirmish", bossSize: null };
const BOSS: MapParameters = { roomCount: 6, encounter: "boss", bossSize: "huge" };
const SKIRMISH_FORM: GenerateButtonInput["form"] = {
  roomCount: 6,
  encounter: "skirmish",
  bossSize: "large",
};
const BOSS_FORM: GenerateButtonInput["form"] = { roomCount: 6, encounter: "boss", bossSize: "huge" };

describe("generateButtonState", () => {
  test("is ready before any map is drawn", () => {
    expect(generateButtonState({ busy: false, drawnParameters: null, form: SKIRMISH_FORM })).toBe(
      "ready",
    );
  });

  test("is rolling while busy, with or without a drawn map", () => {
    expect(generateButtonState({ busy: true, drawnParameters: null, form: SKIRMISH_FORM })).toBe(
      "rolling",
    );
    expect(
      generateButtonState({ busy: true, drawnParameters: SKIRMISH, form: SKIRMISH_FORM }),
    ).toBe("rolling");
    expect(
      generateButtonState({
        busy: true,
        drawnParameters: SKIRMISH,
        form: { ...SKIRMISH_FORM, roomCount: 8 },
      }),
    ).toBe("rolling");
  });

  test("offers a reroll when the form matches the drawn map", () => {
    expect(
      generateButtonState({ busy: false, drawnParameters: SKIRMISH, form: SKIRMISH_FORM }),
    ).toBe("reroll");
    expect(generateButtonState({ busy: false, drawnParameters: BOSS, form: BOSS_FORM })).toBe(
      "reroll",
    );
  });

  test("is ready again when the room count differs", () => {
    expect(
      generateButtonState({
        busy: false,
        drawnParameters: SKIRMISH,
        form: { ...SKIRMISH_FORM, roomCount: 7 },
      }),
    ).toBe("ready");
  });

  test("is ready again when the encounter differs", () => {
    expect(
      generateButtonState({
        busy: false,
        drawnParameters: SKIRMISH,
        form: { ...SKIRMISH_FORM, encounter: "boss" },
      }),
    ).toBe("ready");
    expect(
      generateButtonState({
        busy: false,
        drawnParameters: BOSS,
        form: { ...BOSS_FORM, encounter: "skirmish" },
      }),
    ).toBe("ready");
  });

  test("ignores the hidden boss size on a skirmish", () => {
    // The form keeps its boss size while the field is hidden; a skirmish map echoes null.
    expect(
      generateButtonState({
        busy: false,
        drawnParameters: SKIRMISH,
        form: { ...SKIRMISH_FORM, bossSize: "gargantuan" },
      }),
    ).toBe("reroll");
  });

  test("is ready again when a boss map's size differs", () => {
    expect(
      generateButtonState({
        busy: false,
        drawnParameters: BOSS,
        form: { ...BOSS_FORM, bossSize: "large" },
      }),
    ).toBe("ready");
  });
});
