import { useId, useState, type ReactElement, type ReactNode } from "react";
import { Alert } from "~/components/ui/alert";
import { Button } from "~/components/ui/button";
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
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import type { LibraryError, SavedMap, SavedMapResult } from "~/api/client";
import { hasNonTitleError, libraryErrorMessage, titleErrorOf } from "~/lib/library-errors";

// The API's title rule (api/Maps/SavedMapModels.cs): trimmed, then 1 to 80 characters. The input
// enforces the length and presence; the API's message covers the rest (only spaces, control
// characters).
export const MAX_TITLE_LENGTH = 80;

type MapTitleDialogProps = {
  // The button that opens the dialog; focus returns to it when the dialog closes.
  trigger: ReactElement;
  heading: string;
  description: ReactNode;
  submitLabel: string;
  pendingLabel: string;
  // Completes "The server could not …" in an error message, e.g. "save the map".
  action: string;
  initialTitle: string;
  submit: (title: string) => Promise<SavedMapResult>;
  onSaved: (savedMap: SavedMap) => void;
  // When given, a successful submit shows this instead of closing the dialog.
  renderSuccess?: (savedMap: SavedMap) => ReactNode;
  // When given, a not-found answer (the map is gone) calls this once the dialog has closed, after
  // the user has read the message; focus handling is then the caller's, since the trigger may go.
  onGone?: () => void;
};

// The title form behind Save (home) and Rename (My maps). Errors from the API stay inside the
// dialog: the title message under the field, anything else (the 100-map cap, a lost connection)
// in an alert above it.
export function MapTitleDialog({
  trigger,
  heading,
  description,
  submitLabel,
  pendingLabel,
  action,
  initialTitle,
  submit,
  onSaved,
  renderSuccess,
  onGone,
}: MapTitleDialogProps) {
  const [open, setOpen] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<LibraryError | null>(null);
  const [saved, setSaved] = useState<SavedMap | null>(null);
  const inputId = useId();
  const errorId = useId();
  const titleError = titleErrorOf(error);

  // The dialog stays open while a submit is in flight, so its answer always lands in the session
  // that sent it, never in a closed or reopened dialog.
  function onOpenChange(next: boolean) {
    if (!next && pending) return;
    setOpen(next);
    if (next) {
      setError(null);
      setSaved(null);
    }
  }

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const title = String(new FormData(event.currentTarget).get("title") ?? "");
    setPending(true);
    setError(null);
    const result = await submit(title);
    setPending(false);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    onSaved(result.savedMap);
    if (renderSuccess) setSaved(result.savedMap);
    else setOpen(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent
        onCloseAutoFocus={(event) => {
          if (!onGone || error?.kind !== "not-found") return;
          event.preventDefault();
          onGone();
        }}
      >
        {saved && renderSuccess ? (
          renderSuccess(saved)
        ) : (
          <form onSubmit={onSubmit} className="grid gap-4">
            <DialogHeader>
              <DialogTitle>{heading}</DialogTitle>
              <DialogDescription>{description}</DialogDescription>
            </DialogHeader>
            {hasNonTitleError(error) && (
              <Alert variant="destructive">{libraryErrorMessage(error, action)}</Alert>
            )}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor={inputId}>Title</Label>
              <Input
                id={inputId}
                name="title"
                defaultValue={initialTitle}
                required
                maxLength={MAX_TITLE_LENGTH}
                autoComplete="off"
                aria-invalid={titleError ? true : undefined}
                aria-describedby={titleError ? errorId : undefined}
              />
              {titleError && (
                <p id={errorId} className="text-sm text-destructive">
                  {titleError}
                </p>
              )}
            </div>
            <DialogFooter>
              <DialogClose asChild>
                <Button variant="outline" disabled={pending}>
                  Cancel
                </Button>
              </DialogClose>
              <Button type="submit" disabled={pending}>
                {pending ? pendingLabel : submitLabel}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
