import { useEffect, useRef, useState } from "react";
import type { Route } from "./+types/home";
import { Alert } from "~/components/ui/alert";
import { Button } from "~/components/ui/button";
import { Label } from "~/components/ui/label";
import { NativeSelect, NativeSelectOption } from "~/components/ui/native-select";
import { MapPreview, type MapPreviewState } from "~/components/map-preview";
import type { GenerateError, GeneratedMap } from "~/api/client";
import {
  DEFAULT_ROOM_COUNT,
  MAX_ROOM_COUNT,
  MIN_ROOM_COUNT,
  generateMapWith,
  type BossSize,
  type EncounterType,
} from "~/api/maps";
import { downloadCanvas } from "~/map/download";
import { loadAtlas, renderMap } from "~/map/render";

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

const ENCOUNTERS: { value: EncounterType; label: string }[] = [
  { value: "skirmish", label: "Skirmish" },
  { value: "boss", label: "Boss fight" },
];

const BOSS_SIZES: { value: BossSize; label: string }[] = [
  { value: "large", label: "Large" },
  { value: "huge", label: "Huge" },
  { value: "gargantuan", label: "Gargantuan" },
];

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
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const atlasRef = useRef<Promise<ImageBitmap> | null>(null);
  // The map whose pixels are on the canvas. Rendering finishes after the atlas loads, so a map
  // in state is not yet a map on screen; the download follows this, not the status.
  const [renderedMap, setRenderedMap] = useState<GeneratedMap | null>(null);
  // The map whose drawing is on screen. Unlike renderedMap it survives a new Generate, so the
  // last drawing stays visible (dimmed) while the next one loads; an error clears it.
  const [shownMap, setShownMap] = useState<GeneratedMap | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);
  // The form's values; Generate sends them as they are when it is clicked.
  const [roomCount, setRoomCount] = useState(DEFAULT_ROOM_COUNT);
  const [encounter, setEncounter] = useState<EncounterType>("skirmish");
  const [bossSize, setBossSize] = useState<BossSize>("large");

  const map = status.kind === "ready" ? status.map : null;
  const canDownload = map !== null && renderedMap === map;
  const drawnMap = canDownload ? map : null;
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

  useEffect(() => {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext("2d");
    if (!map || !ctx) return;
    let cancelled = false;
    // A failed load is forgotten so the next map retries it; a render error keeps the good atlas.
    atlasRef.current ??= loadAtlas().catch((error: unknown) => {
      atlasRef.current = null;
      throw error;
    });
    atlasRef.current
      .then((atlas) => {
        if (cancelled) return;
        renderMap(ctx, map, atlas);
        setRenderedMap(map);
        setShownMap(map);
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          const detail = error instanceof Error ? error.message : String(error);
          setShownMap(null);
          setStatus({ kind: "error", message: `Could not draw the map: ${detail}` });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [map]);

  async function onGenerate() {
    setStatus({ kind: "loading" });
    setRenderedMap(null);
    setDownloadError(null);
    const result = await generateMapWith({
      roomCount,
      encounter,
      bossSize: encounter === "boss" ? bossSize : undefined,
    });
    if (!result.ok) setShownMap(null);
    setStatus(
      result.ok
        ? { kind: "ready", map: result.map }
        : { kind: "error", message: errorMessage(result.error) },
    );
  }

  async function onDownload() {
    const canvas = canvasRef.current;
    if (!canDownload || !canvas) return;
    setDownloadError(null);
    try {
      await downloadCanvas(canvas, map.seed);
    } catch (error: unknown) {
      setDownloadError(error instanceof Error ? error.message : String(error));
    }
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
        <Button variant="outline" onClick={onDownload} disabled={!canDownload}>
          Download PNG
        </Button>
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
