import { useEffect, useRef, useState } from "react";
import type { GeneratedMap } from "~/api/client";
import { downloadCanvas } from "./download";
import { loadAtlas, renderMap } from "./render";

// The atlas, shared by every view that draws a map, so moving between the generator and a saved
// map loads it once. A failed load is forgotten so the next map retries it; a render error keeps
// the good atlas.
let atlasPromise: Promise<ImageBitmap> | null = null;

function getAtlas(): Promise<ImageBitmap> {
  atlasPromise ??= loadAtlas().catch((error: unknown) => {
    atlasPromise = null;
    throw error;
  });
  return atlasPromise;
}

// The one canvas pipeline behind the home view and a saved map's view: it draws `map` into the
// canvas behind canvasRef (renderMap, once, at 140 px per square) and downloads that same canvas
// (download.ts). onRenderError receives a message when a map cannot be drawn; the latest callback
// is used, so it need not be stable.
export function useMapCanvas(map: GeneratedMap | null, onRenderError: (message: string) => void) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  // The map whose pixels are on the canvas. Rendering finishes after the atlas loads, so a map
  // passed in is not yet a map on screen; the download follows this, not the argument.
  const [renderedMap, setRenderedMap] = useState<GeneratedMap | null>(null);
  // The map whose drawing is on screen. Unlike renderedMap it survives a new map, so the last
  // drawing stays visible (dimmed) while the next one loads; a render error clears it, and so
  // does forgetShownMap.
  const [shownMap, setShownMap] = useState<GeneratedMap | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const onRenderErrorRef = useRef(onRenderError);
  useEffect(() => {
    onRenderErrorRef.current = onRenderError;
  });

  useEffect(() => {
    const ctx = canvasRef.current?.getContext("2d");
    if (!map || !ctx) return;
    let cancelled = false;
    getAtlas()
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
          onRenderErrorRef.current(`Could not draw the map: ${detail}`);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [map]);

  // Only the map that is actually drawn can be downloaded.
  const drawnMap = map !== null && renderedMap === map ? map : null;

  async function download() {
    const canvas = canvasRef.current;
    if (!drawnMap || !canvas) return;
    setDownloadError(null);
    try {
      await downloadCanvas(canvas, drawnMap.seed);
    } catch (error: unknown) {
      setDownloadError(error instanceof Error ? error.message : String(error));
    }
  }

  return {
    canvasRef,
    drawnMap,
    shownMap,
    forgetShownMap: () => setShownMap(null),
    downloadError,
    clearDownloadError: () => setDownloadError(null),
    download,
  };
}
