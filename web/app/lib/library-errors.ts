import type { LibraryError } from "~/api/client";

// The message a saved-map view shows for a failed library call. action completes "The server
// could not …", e.g. "save the map". An invalid request shows the API's own message; the title
// dialog shows the "title" message next to its field instead (titleErrorOf).
export function libraryErrorMessage(error: LibraryError, action: string): string {
  switch (error.kind) {
    case "not-found":
      return "This map no longer exists. It may have been deleted.";
    case "invalid": {
      const { title: _title, request, ...rest } = error.errors;
      return (
        request?.[0] ??
        Object.values(rest).flat()[0] ??
        `The server could not ${action}. Check the details and try again.`
      );
    }
    case "rate-limited":
      return "Too many requests in a short time. Please wait a minute and try again.";
    case "network":
      return "Could not reach the server. Check your connection and try again.";
    case "http":
      return `The server could not ${action} (error ${error.status}). Please try again.`;
  }
}

// The API's message for the title field, if the error is about the title.
export function titleErrorOf(error: LibraryError | null): string | undefined {
  return error?.kind === "invalid" ? error.errors.title?.[0] : undefined;
}

// Whether an error has anything to say beyond the title field.
export function hasNonTitleError(error: LibraryError | null): error is LibraryError {
  if (!error) return false;
  if (error.kind !== "invalid") return true;
  const keys = Object.keys(error.errors);
  return keys.length === 0 || keys.some((key) => key !== "title");
}
