import { useEffect, useRef, useState } from "react";
import type { Route } from "./+types/home";
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
      <h1 className="text-2xl font-semibold">Battle Map Generator</h1>
      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={onGenerate}
          disabled={status.kind === "loading"}
          className="rounded bg-gray-900 px-4 py-2 text-white disabled:opacity-50 dark:bg-gray-100 dark:text-gray-900"
        >
          {status.kind === "loading" ? "Generating…" : "Generate"}
        </button>
        <button
          type="button"
          onClick={onDownload}
          disabled={!canDownload}
          className="rounded border border-gray-900 px-4 py-2 disabled:opacity-50 dark:border-gray-100"
        >
          Download PNG
        </button>
        {map && <span>Seed: {map.seed}</span>}
      </div>
      {status.kind === "error" && (
        <p role="alert" className="text-red-700 dark:text-red-400">
          {status.message}
        </p>
      )}
      {downloadError && (
        <p role="alert" className="text-red-700 dark:text-red-400">
          {downloadError}
        </p>
      )}
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
