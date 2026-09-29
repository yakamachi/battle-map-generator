const REVOKE_DELAY_MS = 60_000;

export function downloadFilename(seed: number): string {
  return `battle-map-${seed}.png`;
}

// Saves the rendered canvas itself, so the file is exactly the bitmap the preview shows.
// Rejects instead of saving an empty file when the browser cannot encode the canvas.
export function downloadCanvas(canvas: HTMLCanvasElement, seed: number): Promise<void> {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (!blob) {
        reject(new Error("The browser could not export the map as a PNG."));
        return;
      }
      // This callback runs outside the executor, so its errors must be passed to reject by hand.
      try {
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = downloadFilename(seed);
        // Firefox only follows a click on an anchor that is in the document.
        document.body.append(link);
        link.click();
        link.remove();
        // The browser may read the blob after click() returns (Firefox does); revoking too soon
        // cancels the download, so the URL is kept for a while. It leaks one blob per click at most.
        setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS);
        resolve();
      } catch (error) {
        reject(error);
      }
    }, "image/png");
  });
}
