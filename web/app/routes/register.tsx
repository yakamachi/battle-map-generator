import { useState } from "react";
import { Link, redirect, useNavigate } from "react-router";
import type { Route } from "./+types/register";
import { Alert } from "~/components/ui/alert";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { getSession, register, type RegisterError } from "~/api/auth";

export function meta({}: Route.MetaArgs) {
  return [{ title: "Register — Battle Map Generator" }];
}

// A logged-in visitor never sees the register form.
export async function clientLoader() {
  const account = await getSession();
  if (account) throw redirect("/");
  return null;
}

type Status = { kind: "idle" } | { kind: "submitting" } | { kind: "error"; message: string };

function errorMessage(error: RegisterError): string {
  switch (error.kind) {
    case "validation":
      return (
        Object.values(error.errors).flat().join(" ") || "Please check your details and try again."
      );
    case "rate-limited":
      return "Too many attempts. Please wait a minute and try again.";
    case "network":
      return "Could not reach the server. Check your connection and try again.";
    case "http":
      // A waking database surfaces as a 500 (phase 1 review F7).
      return error.status >= 500
        ? "The server is not ready yet. Try again in a minute."
        : `The server could not create your account (error ${error.status}). Please try again.`;
  }
}

export default function Register() {
  const navigate = useNavigate();
  const [status, setStatus] = useState<Status>({ kind: "idle" });
  const submitting = status.kind === "submitting";

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const email = String(form.get("email") ?? "");
    const password = String(form.get("password") ?? "");
    setStatus({ kind: "submitting" });
    const result = await register(email, password);
    if (result.ok) {
      navigate("/");
      return;
    }
    setStatus({ kind: "error", message: errorMessage(result.error) });
  }

  return (
    <main className="container mx-auto flex max-w-sm flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold tracking-tight text-foreground">Create an account</h1>
      {status.kind === "error" && <Alert variant="destructive">{status.message}</Alert>}
      <form onSubmit={onSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="email">Email</Label>
          <Input id="email" name="email" type="email" autoComplete="email" required />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="password">Password</Label>
          <Input id="password" name="password" type="password" autoComplete="new-password" required />
          <p className="text-xs text-muted-foreground">At least 8 characters</p>
        </div>
        <Button type="submit" disabled={submitting}>
          {submitting ? "Creating account…" : "Create account"}
        </Button>
      </form>
      <p className="text-sm text-muted-foreground">
        Already have an account?{" "}
        <Link to="/login" className="underline underline-offset-4 hover:text-foreground">
          Log in
        </Link>
      </p>
    </main>
  );
}
