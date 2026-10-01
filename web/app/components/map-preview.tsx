import type { CSSProperties, Ref } from "react";
import { cn } from "cn";
import { TILE_SIZE } from "~/map/tileset";

export type MapPreviewState = "empty" | "loading" | "ready";

type MapPreviewProps = {
  canvasRef: Ref<HTMLCanvasElement>;
  // The map's size in squares; the frame takes its aspect ratio. 30×20 before any map.
  width?: number;
  height?: number;
  state: MapPreviewState;
  // Whether the canvas holds a drawing that may be shown (false after an error).
  drawn: boolean;
  // The canvas's accessible name, while it holds a map.
  label?: string;
};

// The preview frame. The single canvas is always mounted and only CSS sizes it on screen: its
// bitmap (width/height attributes) belongs to renderMap, so the frame never resizes the page.
export function MapPreview({
  canvasRef,
  width = 30,
  height = 20,
  state,
  drawn,
  label,
}: MapPreviewProps) {
  const loading = state === "loading";
  const frameStyle = {
    aspectRatio: `${width} / ${height}`,
    "--map-aspect": width / height,
  } as CSSProperties;

  return (
    <div
      aria-busy={loading || undefined}
      className="map-preview-fit relative mx-auto overflow-hidden rounded-lg border border-border bg-card text-card-foreground"
      style={frameStyle}
    >
      <canvas
        ref={canvasRef}
        role="img"
        aria-label={label}
        className={cn(
          "absolute inset-0 size-full object-contain transition-opacity",
          !drawn && "hidden",
          loading && "opacity-40",
        )}
      />
      {!drawn && !loading && (
        <p className="absolute inset-0 flex items-center justify-center p-4 text-center text-sm text-muted-foreground">
          Click Generate to create a battle map, then download it as a PNG ({TILE_SIZE} px per
          square)
        </p>
      )}
      {loading && (
        <div className="absolute inset-0 flex items-center justify-center p-4">
          <span className="rounded-md bg-card/80 px-3 py-1.5 text-sm font-medium text-card-foreground">
            Generating…
          </span>
        </div>
      )}
    </div>
  );
}
