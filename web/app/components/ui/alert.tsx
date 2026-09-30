import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "cn";

// One error and notice primitive. The children carry the message, so meaning never relies on
// colour alone: the text says what went wrong.
const alertVariants = cva("w-full rounded-lg border px-4 py-3 text-sm", {
  variants: {
    variant: {
      default: "border-border bg-card text-card-foreground",
      destructive: "border-destructive/30 bg-destructive/10 text-destructive",
    },
  },
  defaultVariants: {
    variant: "default",
  },
});

function Alert({
  className,
  variant = "default",
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof alertVariants>) {
  return (
    <div
      // Errors interrupt (assertive); notices wait their turn (polite). Callers may override.
      role={variant === "destructive" ? "alert" : "status"}
      data-slot="alert"
      data-variant={variant}
      className={cn(alertVariants({ variant, className }))}
      {...props}
    />
  );
}

export { Alert, alertVariants };
