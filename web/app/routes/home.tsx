import { useState } from "react";
import { Link } from "react-router";
import type { Route } from "./+types/home";
import { Alert } from "~/components/ui/alert";
import { Button, buttonVariants } from "~/components/ui/button";
import { DialogClose, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "~/components/ui/dialog";
import { Label } from "~/components/ui/label";
import { NativeSelect, NativeSelectOption } from "~/components/ui/native-select";
import { MapPreview, type MapPreviewState } from "~/components/map-preview";
import { MapTitleDialog } from "~/components/map-title-dialog";
import {
  DEFAULT_ROOM_COUNT,
  MAX_ROOM_COUNT,
  MIN_ROOM_COUNT,
  generateMap,
  saveMap,
  type BossSize,
  type EncounterType,
  type GenerateError,
  type GeneratedMap,
} from "~/api/client";
import { BOSS_SIZES, ENCOUNTERS } from "~/lib/map-labels";
import { useMapCanvas } from "~/map/use-map-canvas";
import { cn } from "cn";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Battle Map Generator" },
    { name: "description", content: "Generate grid-aligned D&D battle maps." },
  ];
}

type Status =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "ready"; map: GeneratedMap }
  | { kind: "error"; message: string };

const ROOM_COUNTS = Array.from(
  { length: MAX_ROOM_COUNT - MIN_ROOM_COUNT + 1 },
  (_, i) => MIN_ROOM_COUNT + i,
);

function errorMessage(error: GenerateError): string {
  switch (error.kind) {
    case "rate-limited":
      return "Too many maps in a short time. Please wait a minute and try again.";
    case "network":
      return "Could not reach the server. Check your connection and try again.";
    case "http":
      // A 400 means the API rejected the settings, so trying again would not help.
      return error.status === 400
        ? "These map settings are not supported. Change them and generate again."
        : `The server could not generate a map (error ${error.status}). Please try again.`;
  }
}

export default function Home() {
  const [status, setStatus] = useState<Status>({ kind: "idle" });
  // The form's values; Generate sends them as they are when it is clicked.
  const [roomCount, setRoomCount] = useState(DEFAULT_ROOM_COUNT);
  const [encounter, setEncounter] = useState<EncounterType>("skirmish");
  const [bossSize, setBossSize] = useState<BossSize>("large");

  const map = status.kind === "ready" ? status.map : null;
  const { canvasRef, drawnMap, shownMap, forgetShownMap, downloadError, clearDownloadError, download } =
    useMapCanvas(map, (message) => setStatus({ kind: "error", message }));
  const canDownload = drawnMap !== null;
  // A map in state is not on screen until the atlas has loaded and it is drawn.
  const previewState: MapPreviewState =
    status.kind === "loading" || (status.kind === "ready" && !canDownload)
      ? "loading"
      : status.kind === "ready"
        ? "ready"
        : "empty";
  // One busy state for the button and the frame: a map is not done until it is drawn.
  const busy = previewState === "loading";
  const previewSize = map ?? shownMap;

  async function onGenerate() {
    setStatus({ kind: "loading" });
    clearDownloadError();
    const result = await generateMap({
      roomCount,
      encounter,
      bossSize: encounter === "boss" ? bossSize : undefined,
    });
    if (!result.ok) forgetShownMap();
    setStatus(
      result.ok
        ? { kind: "ready", map: result.map }
        : { kind: "error", message: errorMessage(result.error) },
    );
  }

  // Save sends the drawn map's seed and parameters; the server regenerates the map from them.
  function onSave(title: string) {
    if (!drawnMap) throw new Error("Save is enabled only while a map is drawn.");
    const { seed, parameters } = drawnMap;
    return saveMap({ title, seed, ...parameters });
  }

  return (
    // Rows above the preview count against its height: update map-preview-fit in app.css when
    // adding one.
    <main className="container mx-auto flex flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">
        Battle Map Generator
      </h1>
      {/* Each field is a group, so its Label dims with the select while a map is generating. */}
      <div className="flex flex-wrap items-center gap-4">
        <div className="group flex items-center gap-2" data-disabled={busy}>
          <Label htmlFor="room-count">Rooms</Label>
          <NativeSelect
            id="room-count"
            value={roomCount}
            onChange={(event) => setRoomCount(Number(event.target.value))}
            disabled={busy}
          >
            {ROOM_COUNTS.map((count) => (
              <NativeSelectOption key={count} value={count}>
                {count}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
        <div className="group flex items-center gap-2" data-disabled={busy}>
          <Label htmlFor="encounter">Encounter</Label>
          <NativeSelect
            id="encounter"
            value={encounter}
            onChange={(event) => setEncounter(event.target.value as EncounterType)}
            disabled={busy}
          >
            {ENCOUNTERS.map(({ value, label }) => (
              <NativeSelectOption key={value} value={value}>
                {label}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
        {encounter === "boss" && (
          <div className="group flex items-center gap-2" data-disabled={busy}>
            <Label htmlFor="boss-size">Boss size</Label>
            <NativeSelect
              id="boss-size"
              value={bossSize}
              onChange={(event) => setBossSize(event.target.value as BossSize)}
              disabled={busy}
            >
              {BOSS_SIZES.map(({ value, label }) => (
                <NativeSelectOption key={value} value={value}>
                  {label}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-4">
        <Button onClick={onGenerate} disabled={busy}>
          {busy ? "Generating…" : "Generate"}
        </Button>
        <Button variant="outline" onClick={download} disabled={!canDownload}>
          Download PNG
        </Button>
        {/* The dialog is a portal over the page, so it adds no row above the preview. */}
        <MapTitleDialog
          trigger={
            <Button variant="outline" disabled={!canDownload}>
              Save
            </Button>
          }
          heading="Save map"
          description="Give this map a title to find it again in My maps."
          submitLabel="Save"
          pendingLabel="Saving…"
          action="save the map"
          initialTitle=""
          submit={onSave}
          // The stored map is the server's regeneration of this seed; show that one.
          onSaved={(saved) => setStatus({ kind: "ready", map: saved.map })}
          renderSuccess={(saved) => (
            <>
              <DialogHeader>
                <DialogTitle>Map saved</DialogTitle>
                <DialogDescription>“{saved.title}” is in My maps.</DialogDescription>
              </DialogHeader>
              <DialogFooter>
                <DialogClose asChild>
                  <Button variant="outline" autoFocus>
                    Close
                  </Button>
                </DialogClose>
                <Link to={`/maps/${saved.id}`} className={cn(buttonVariants())}>
                  Open saved map
                </Link>
              </DialogFooter>
            </>
          )}
        />
        {drawnMap && (
          <span className="text-sm text-muted-foreground tabular-nums">Seed: {drawnMap.seed}</span>
        )}
      </div>
      {status.kind === "error" && <Alert variant="destructive">{status.message}</Alert>}
      {downloadError && <Alert variant="destructive">{downloadError}</Alert>}
      <MapPreview
        canvasRef={canvasRef}
        width={previewSize?.width}
        height={previewSize?.height}
        state={previewState}
        drawn={shownMap !== null}
        label={shownMap ? `Battle map, seed ${shownMap.seed}` : undefined}
      />
    </main>
  );
}
