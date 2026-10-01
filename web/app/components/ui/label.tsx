import * as React from "react";
import { cn } from "cn";

// Adapted from shadcn's radix-nova Label: a native <label> only. The generated component wraps
// Radix's Label primitive, but this repo has no radix-ui dependency (button.tsx and alert.tsx
// are native elements for the same reason); a plain <label htmlFor> already associates with and
// focuses its input without it.
function Label({ className, ...props }: React.ComponentProps<"label">) {
  return (
    <label
      data-slot="label"
      className={cn(
        "flex items-center gap-2 text-sm leading-none font-medium select-none peer-disabled:cursor-not-allowed peer-disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}

export { Label };
