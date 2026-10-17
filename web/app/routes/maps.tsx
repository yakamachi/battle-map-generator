import { useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import type { Route } from "./+types/maps";
import { Alert } from "~/components/ui/alert";
import { Button, buttonVariants } from "~/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "~/components/ui/dialog";
import { MapTitleDialog } from "~/components/map-title-dialog";
import {
  deleteSavedMap,
  listSavedMaps,
  renameSavedMap,
  type LibraryError,
  type SavedMap,
  type SavedMapSummary,
} from "~/api/client";
import { libraryErrorMessage } from "~/lib/library-errors";
import { encounterLabel, sizeLabel } from "~/lib/map-labels";
import { cn } from "cn";

export function meta({}: Route.MetaArgs) {
  return [{ title: "My maps — Battle Map Generator" }];
}

// The API's cap (SavedMapEndpoints.MaxSavedMapsPerAccount); Save answers 400 at it.
const MAX_SAVED_MAPS = 100;

// The viewer's locale and time zone; the visual gate pins both.
const DATE_FORMAT = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });

type Status =
  | { kind: "loading" }
  | { kind: "ready"; maps: SavedMapSummary[] }
  | { kind: "error"; message: string };

// The list loads after the protected layout's session check, not in a clientLoader: loaders run in
// parallel, so a logged-out visitor's 401 here would race the layout's redirect to /login.
export default function Maps() {
  const [status, setStatus] = useState<Status>({ kind: "loading" });
  const headingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    let cancelled = false;
    listSavedMaps().then((result) => {
      if (cancelled) return;
      setStatus(
        result.ok
          ? { kind: "ready", maps: result.maps }
          : { kind: "error", message: libraryErrorMessage(result.error, "load your maps") },
      );
    });
    return () => {
      cancelled = true;
    };
  }, []);

  // A rename changes only the title (and updatedAt); the list order is by creation, so it stays.
  function onRenamed(saved: SavedMap) {
    setStatus((current) =>
      current.kind === "ready"
        ? {
            kind: "ready",
            maps: current.maps.map((summary) =>
              summary.id === saved.id
                ? { ...summary, title: saved.title, updatedAt: saved.updatedAt }
                : summary,
            ),
          }
        : current,
    );
  }

  // The deleted row's Delete button is gone, so focus moves to the heading instead.
  function onDeleted(id: string) {
    setStatus((current) =>
      current.kind === "ready"
        ? { kind: "ready", maps: current.maps.filter((summary) => summary.id !== id) }
        : current,
    );
    headingRef.current?.focus();
  }

  return (
    <main className="container mx-auto flex flex-col gap-4 p-4">
      <h1
        ref={headingRef}
        tabIndex={-1}
        className="text-2xl font-semibold tracking-tight text-foreground outline-none"
      >
        My maps
      </h1>
      {status.kind === "loading" && (
        <p role="status" className="text-sm text-muted-foreground">
          Loading your maps…
        </p>
      )}
      {status.kind === "error" && <Alert variant="destructive">{status.message}</Alert>}
      {status.kind === "ready" && status.maps.length === 0 && (
        <div className="flex flex-col items-start gap-4 rounded-lg border border-dashed border-border p-6">
          <p className="text-sm text-muted-foreground">
            You have no saved maps yet. Generate a map and click Save to keep it here.
          </p>
          <Link to="/" className={cn(buttonVariants({ variant: "outline" }))}>
            Back to the generator
          </Link>
        </div>
      )}
      {status.kind === "ready" && status.maps.length > 0 && (
        <>
          <p className="text-sm text-muted-foreground">
            {status.maps.length} of {MAX_SAVED_MAPS} saved maps, newest first.
          </p>
          <ul className="flex flex-col gap-3">
            {status.maps.map((summary) => (
              <MapRow
                key={summary.id}
                summary={summary}
                onRenamed={onRenamed}
                onDeleted={onDeleted}
              />
            ))}
          </ul>
        </>
      )}
    </main>
  );
}

function MapRow({
  summary,
  onRenamed,
  onDeleted,
}: {
  summary: SavedMapSummary;
  onRenamed: (saved: SavedMap) => void;
  onDeleted: (id: string) => void;
}) {
  return (
    <li className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 text-card-foreground sm:flex-row sm:items-center sm:justify-between">
      <div className="flex min-w-0 flex-col gap-1">
        <h2 className="font-medium break-words">{summary.title}</h2>
        <p className="text-sm text-muted-foreground tabular-nums">
          {sizeLabel(summary)} · {encounterLabel(summary.parameters)} · Saved{" "}
          <time dateTime={summary.createdAt}>{DATE_FORMAT.format(new Date(summary.createdAt))}</time>
        </p>
      </div>
      {/* The hidden title gives each row's buttons a unique accessible name. */}
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        <Link to={`/maps/${summary.id}`} className={cn(buttonVariants({ variant: "outline" }))}>
          Open<span className="sr-only"> {summary.title}</span>
        </Link>
        <MapTitleDialog
          trigger={
            <Button variant="outline">
              Rename<span className="sr-only"> {summary.title}</span>
            </Button>
          }
          heading="Rename map"
          description="Change the title this map is listed under."
          submitLabel="Rename"
          pendingLabel="Renaming…"
          action="rename the map"
          initialTitle={summary.title}
          submit={(title) => renameSavedMap(summary.id, title)}
          onSaved={onRenamed}
        />
        <DeleteMapDialog summary={summary} onDeleted={onDeleted} />
      </div>
    </li>
  );
}

function DeleteMapDialog({
  summary,
  onDeleted,
}: {
  summary: SavedMapSummary;
  onDeleted: (id: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<LibraryError | null>(null);
  // Set once the map is gone, so the dialog's close hands focus to the list instead of the
  // trigger, which is removed with the row.
  const deleted = useRef(false);

  function onOpenChange(next: boolean) {
    setOpen(next);
    if (next) setError(null);
  }

  async function onConfirm() {
    setPending(true);
    setError(null);
    const result = await deleteSavedMap(summary.id);
    setPending(false);
    // A map that is already gone (deleted in another tab) leaves the list too.
    if (result.ok || result.error.kind === "not-found") {
      deleted.current = true;
      setOpen(false);
      return;
    }
    setError(result.error);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>
        <Button variant="destructive">
          Delete<span className="sr-only"> {summary.title}</span>
        </Button>
      </DialogTrigger>
      <DialogContent
        // The row is removed only after the dialog has closed, so focus is never left inside a
        // removed dialog.
        onCloseAutoFocus={(event) => {
          if (!deleted.current) return;
          event.preventDefault();
          onDeleted(summary.id);
        }}
      >
        <DialogHeader>
          <DialogTitle>Delete map?</DialogTitle>
          <DialogDescription className="break-words">
            “{summary.title}” will be deleted from My maps. This cannot be undone.
          </DialogDescription>
        </DialogHeader>
        {error && (
          <Alert variant="destructive">{libraryErrorMessage(error, "delete the map")}</Alert>
        )}
        <DialogFooter>
          <DialogClose asChild>
            <Button variant="outline">Cancel</Button>
          </DialogClose>
          <Button variant="destructive" onClick={onConfirm} disabled={pending}>
            {pending ? "Deleting…" : "Delete"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
