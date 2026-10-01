import { Outlet, redirect, useNavigate } from "react-router";
import type { Route } from "./+types/protected";
import { Button } from "~/components/ui/button";
import { getSession, logout } from "~/api/auth";

// Every route under this layout requires a session; home.tsx stays unaware of auth entirely.
// SPA mode runs every matched route's clientLoader (this one included) before the first render,
// behind root.tsx's HydrateFallback: React Router permits HydrateFallback only on the root route
// in SPA mode, so the "render nothing until the session check resolves" fallback lives there.
export async function clientLoader() {
  const account = await getSession();
  if (!account) throw redirect("/login");
  return account;
}

export default function ProtectedLayout({ loaderData }: Route.ComponentProps) {
  const navigate = useNavigate();

  async function onLogout() {
    await logout();
    navigate("/login");
  }

  return (
    <>
      {/* A row added here counts against map-preview-fit in app.css. */}
      <header className="border-b border-border bg-card">
        <div className="container mx-auto flex items-center justify-between gap-4 px-4 py-3">
          <span className="text-sm text-muted-foreground">{loaderData.email}</span>
          <Button variant="outline" onClick={onLogout}>
            Log out
          </Button>
        </div>
      </header>
      <Outlet />
    </>
  );
}
