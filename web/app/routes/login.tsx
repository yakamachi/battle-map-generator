import { useState } from "react";
import { Link, redirect, useNavigate, useSearchParams } from "react-router";
import type { Route } from "./+types/login";
import { Alert } from "~/components/ui/alert";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { getSession, login, type LoginError } from "~/api/auth";

export function meta({}: Route.MetaArgs) {
  return [{ title: "Log in — Battle Map Generator" }];
}

// A logged-in visitor never sees the login form.
export async function clientLoader() {
  const account = await getSession();
  if (account) throw redirect("/");
  return null;
}

function errorMessage(error: LoginError): string {
  switch (error.kind) {
    case "invalid-credentials":
      return "Incorrect email or password.";
    case "rate-limited":
      return "Too many attempts. Please wait a minute and try again.";
    case "network":
      return "Could not reach the server. Check your connection and try again.";
    case "http":
      // A waking database surfaces as a 500 (phase 1 review F7).
      return error.status >= 500
        ? "The server is not ready yet. Try again in a minute."
        : `The server could not log you in (error ${error.status}). Please try again.`;
  }
}

export default function Login() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const expired = searchParams.get("expired") === "1";
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const email = String(form.get("email") ?? "");
    const password = String(form.get("password") ?? "");
    setSubmitting(true);
    setError(null);
    const result = await login(email, password);
    if (result.ok) {
      navigate("/");
      return;
    }
    setSubmitting(false);
    setError(errorMessage(result.error));
  }

  return (
    <main className="container mx-auto flex max-w-sm flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">Log in</h1>
      {expired && (
        <Alert>Your session ended, or the server was waking up. Please log in again.</Alert>
      )}
      {error && <Alert variant="destructive">{error}</Alert>}
      <form onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="email">Email</Label>
          <Input id="email" name="email" type="email" autoComplete="email" required />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="password">Password</Label>
          <Input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
          />
        </div>
        <Button type="submit" disabled={submitting}>
          {submitting ? "Logging in…" : "Log in"}
        </Button>
      </form>
      <p className="text-sm text-muted-foreground">
        Need an account?{" "}
        <Link to="/register" className="underline underline-offset-4 hover:text-foreground">
          Register
        </Link>
      </p>
    </main>
  );
}
