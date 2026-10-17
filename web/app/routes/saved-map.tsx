import { useEffect, useState } from "react";
import { Link } from "react-router";
import type { Route } from "./+types/saved-map";
import { Alert } from "~/components/ui/alert";
import { Button, buttonVariants } from "~/components/ui/button";
import { MapPreview } from "~/components/map-preview";
import { getSavedMap, type SavedMap } from "~/api/client";
import { libraryErrorMessage } from "~/lib/library-errors";
import { encounterLabel, sizeLabel } from "~/lib/map-labels";
import { useMapCanvas } from "~/map/use-map-canvas";
import { cn } from "cn";

export function meta({}: Route.MetaArgs) {
  return [{ title: "Saved map — Battle Map Generator" }];
}

type Status =
  | { kind: "loading" }
  | { kind: "ready"; savedMap: SavedMap }
  | { kind: "not-found" }
  | { kind: "error"; message: string };

// One saved map, drawn and downloaded by the same pipeline as home (useMapCanvas), so it looks
// exactly as it did when it was generated. Another account's map is a 404 like a missing one.
// Loaded after the layout's session check, as in maps.tsx.
export default function SavedMapView({ params }: Route.ComponentProps) {
  const [status, setStatus] = useState<Status>({ kind: "loading" });
  const [renderError, setRenderError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setStatus({ kind: "loading" });
    setRenderError(null);
    getSavedMap(params.id).then((result) => {
      if (cancelled) return;
      if (result.ok) setStatus({ kind: "ready", savedMap: result.savedMap });
      else if (result.error.kind === "not-found") setStatus({ kind: "not-found" });
      else setStatus({ kind: "error", message: libraryErrorMessage(result.error, "load the map") });
    });
    return () => {
      cancelled = true;
    };
  }, [params.id]);

  const savedMap = status.kind === "ready" ? status.savedMap : null;
  const map = savedMap?.map ?? null;
  const { canvasRef, drawnMap, shownMap, downloadError, download } = useMapCanvas(
    map,
    setRenderError,
  );

  if (status.kind === "not-found") {
    return (
      <main className="container mx-auto flex flex-col items-start gap-4 p-4">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">Map not found</h1>
        <p className="text-muted-foreground">
          This map does not exist. It may have been deleted.
        </p>
        <Link to="/maps" className={cn(buttonVariants({ variant: "outline" }))}>
          Back to My maps
        </Link>
      </main>
    );
  }

  if (status.kind === "error") {
    return (
      <main className="container mx-auto flex flex-col items-start gap-4 p-4">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">
          Could not load the map
        </h1>
        <Alert variant="destructive">{status.message}</Alert>
        <Link to="/maps" className={cn(buttonVariants({ variant: "outline" }))}>
          Back to My maps
        </Link>
      </main>
    );
  }

  return (
    // Fewer rows than home sit above the preview (heading and one action row), so the frame
    // fits within home's map-preview-fit budget.
    <main className="container mx-auto flex flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold tracking-tight break-words text-foreground">
        {savedMap?.title ?? "Loading map…"}
      </h1>
      <div className="flex flex-wrap items-center gap-4">
        <Button variant="outline" onClick={download} disabled={!drawnMap}>
          Download PNG
        </Button>
        {drawnMap && (
          <>
            <span className="text-sm text-muted-foreground tabular-nums">
              Seed: {drawnMap.seed}
            </span>
            <span className="text-sm text-muted-foreground tabular-nums">
              {sizeLabel(drawnMap)} · {encounterLabel(drawnMap.parameters)}
            </span>
          </>
        )}
      </div>
      {renderError && <Alert variant="destructive">{renderError}</Alert>}
      {downloadError && <Alert variant="destructive">{downloadError}</Alert>}
      {!renderError && (
        <MapPreview
          canvasRef={canvasRef}
          width={map?.width}
          height={map?.height}
          state={drawnMap ? "ready" : "loading"}
          drawn={shownMap !== null}
          label={shownMap ? `Battle map, seed ${shownMap.seed}` : undefined}
          loadingLabel="Loading map…"
        />
      )}
    </main>
  );
}
