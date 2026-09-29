import { useEffect, useRef, useState } from "react";
import type { Route } from "./+types/home";
import { Alert } from "~/components/ui/alert";
import { Button } from "~/components/ui/button";
import { generateMap, type GenerateError, type GeneratedMap } from "~/api/client";
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

function errorMessage(error: GenerateError): string {
  switch (error.kind) {
    case "rate-limited":
      return "Too many maps in a short time. Please wait a minute and try again.";
    case "network":
      return "Could not reach the server. Check your connection and try again.";
    case "http":
      return `The server could not generate a map (error ${error.status}). Please try again.`;
  }
}

export default function Home() {
  const [status, setStatus] = useState<Status>({ kind: "idle" });
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const atlasRef = useRef<Promise<ImageBitmap> | null>(null);
  // The map whose pixels are on the canvas. Rendering finishes after the atlas loads, so a map
  // in state is not yet a map on screen; the download follows this, not the status.
  const [renderedMap, setRenderedMap] = useState<GeneratedMap | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const map = status.kind === "ready" ? status.map : null;
  const canDownload = map !== null && renderedMap === map;

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
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          const detail = error instanceof Error ? error.message : String(error);
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
    const result = await generateMap();
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
    <main className="container mx-auto flex flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">
        Battle Map Generator
      </h1>
      <div className="flex items-center gap-4">
        <Button onClick={onGenerate} disabled={status.kind === "loading"}>
          {status.kind === "loading" ? "Generating…" : "Generate"}
        </Button>
        <Button variant="outline" onClick={onDownload} disabled={!canDownload}>
          Download PNG
        </Button>
        {map && (
          <span className="text-sm text-muted-foreground tabular-nums">Seed: {map.seed}</span>
        )}
      </div>
      {status.kind === "error" && <Alert variant="destructive">{status.message}</Alert>}
      {downloadError && <Alert variant="destructive">{downloadError}</Alert>}
      {map && (
        <canvas
          ref={canvasRef}
          aria-label={`Battle map, seed ${map.seed}`}
          style={{ maxWidth: "100%", height: "auto" }}
        />
      )}
    </main>
  );
}
