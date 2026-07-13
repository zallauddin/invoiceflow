/**
 * AuthGuard — client component that redirects unauthenticated users.
 * Prevents flash of content by not rendering children until auth check completes.
 */
"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { isAuthenticated } from "@/lib/api";

export function AuthGuard({
  children,
  loginPath = "/login",
}: {
  children: React.ReactNode;
  loginPath?: string;
}) {
  const pathname = usePathname();
  const router = useRouter();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (!mounted) return;

    if (pathname !== loginPath && !isAuthenticated()) {
      router.replace(loginPath);
    } else if (pathname === loginPath && isAuthenticated()) {
      router.replace("/");
    }
  }, [mounted, pathname, loginPath, router]);

  // Don't render anything until mounted to prevent flash
  if (!mounted) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin h-8 w-8 border-4 border-primary border-t-transparent rounded-full" />
      </div>
    );
  }

  // On login page, render children directly (no sidebar)
  if (pathname === loginPath) {
    return <>{children}</>;
  }

  // For authenticated users, still show loading while redirect resolves
  if (!isAuthenticated()) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin h-8 w-8 border-4 border-primary border-t-transparent rounded-full" />
      </div>
    );
  }

  return <>{children}</>;
}
