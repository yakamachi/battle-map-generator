import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "cn";

// Adapted from shadcn's radix-nova Button: a native <button> only (no Slot/asChild), and only the
// variants the app uses. Add variants here, token classes only, when a view needs one.
const buttonVariants = cva(
  "group/button inline-flex shrink-0 items-center justify-center rounded-lg border border-transparent bg-clip-padding text-sm font-medium whitespace-nowrap transition-all outline-hidden select-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 active:not-aria-[haspopup]:translate-y-px disabled:pointer-events-none disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground hover:bg-primary/80",
        outline:
          "border-border bg-background hover:bg-muted hover:text-foreground dark:border-input dark:bg-input/30 dark:hover:bg-input/50",
        // The tint stays at 10% on hover (the border darkens instead): a deeper tint drops the
        // dark-theme text contrast under 4.5 (saved-maps-library tokens.md).
        destructive:
          "border-destructive/30 bg-destructive/10 text-destructive hover:border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20",
        // A quieter action that is still the next step (home's Reroll). The fill stays on hover and
        // the border takes the ring colour; focus-visible keeps the base ring, as outline does.
        // Contrast: regenerate-with-new-seed tokens.md.
        accent: "border-border bg-accent text-accent-foreground hover:border-ring",
      },
      size: {
        default: "h-8 gap-1.5 px-2.5",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  },
);

function Button({
  className,
  variant = "default",
  size = "default",
  type = "button",
  ...props
}: React.ComponentProps<"button"> & VariantProps<typeof buttonVariants>) {
  return (
    <button
      type={type}
      data-slot="button"
      data-variant={variant}
      data-size={size}
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    />
  );
}

export { Button, buttonVariants };
