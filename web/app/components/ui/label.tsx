import * as React from "react";
import { cn } from "cn";

// Adapted from shadcn's radix-nova Label: a native <label> instead of radix-ui's Label.Root, so the
// app needs no radix-ui dependency. Associate it with its control through htmlFor.
function Label({ className, ...props }: React.ComponentProps<"label">) {
  return (
    <label
      data-slot="label"
      className={cn(
        "flex items-center gap-2 text-sm leading-none font-medium select-none group-data-[disabled=true]:pointer-events-none group-data-[disabled=true]:opacity-50 peer-disabled:cursor-not-allowed peer-disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}

export { Label };
