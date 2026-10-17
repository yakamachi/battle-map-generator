import { useState } from "react";
import { NavLink, Outlet, redirect, useNavigate } from "react-router";
import type { Route } from "./+types/protected";
import { Alert } from "~/components/ui/alert";
import { Button } from "~/components/ui/button";
import { getSession, logout } from "~/api/auth";
import { cn } from "cn";

// Every route under this layout (home, My maps, a saved map) requires a session; the views stay
// unaware of auth entirely.
// SPA mode runs every matched route's clientLoader (this one included) before the first render,
// behind root.tsx's HydrateFallback: React Router permits HydrateFallback only on the root route
// in SPA mode, so the "render nothing until the session check resolves" fallback lives there.
export async function clientLoader() {
  const account = await getSession();
  if (!account) throw redirect("/login");
  return account;
}

// The current page's link reads as the foreground; the others are muted until hovered.
function navLinkClass({ isActive }: { isActive: boolean }) {
  return cn(
    "rounded-sm text-sm font-medium underline-offset-4 hover:text-foreground hover:underline focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none",
    isActive ? "text-foreground" : "text-muted-foreground",
  );
}

export default function ProtectedLayout({ loaderData }: Route.ComponentProps) {
  const navigate = useNavigate();
  const [logoutError, setLogoutError] = useState<string | null>(null);

  async function onLogout() {
    setLogoutError(null);
    const result = await logout();
    // A failed logout leaves the session cookie in place: navigating anyway would just bounce the
    // user straight back here once login.tsx's clientLoader sees they're still signed in.
    if (!result.ok) {
      setLogoutError("Could not log out. Check your connection and try again.");
      return;
    }
    navigate("/login");
  }

  return (
    <>
      {/* A row added here counts against map-preview-fit in app.css. The email truncates, so the
          links and Log out stay on one row at phone width. */}
      <header className="border-b border-border bg-card">
        <div className="container mx-auto flex items-center justify-between gap-4 px-4 py-3">
          <nav aria-label="Main" className="flex shrink-0 items-center gap-4">
            <NavLink to="/" end className={navLinkClass}>
              Generator
            </NavLink>
            <NavLink to="/maps" className={navLinkClass}>
              My maps
            </NavLink>
          </nav>
          <div className="flex min-w-0 items-center gap-4">
            <span className="truncate text-sm text-muted-foreground">{loaderData.email}</span>
            <Button variant="outline" onClick={onLogout} className="shrink-0">
              Log out
            </Button>
          </div>
        </div>
      </header>
      {logoutError && (
        <div className="container mx-auto px-4 pt-4">
          <Alert variant="destructive">{logoutError}</Alert>
        </div>
      )}
      <Outlet />
    </>
  );
}
