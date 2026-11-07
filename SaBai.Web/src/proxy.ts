import { NextResponse, type NextRequest } from "next/server";
import { SESSION_COOKIE_NAME } from "@/lib/api/session";

/**
 * Route protection for the web app.
 *
 * SaBai.API keeps the session in the HttpOnly `sabai.auth` cookie, which is
 * first-party because the API is reached through the /api rewrite. This
 * middleware only checks that the cookie is present: the session itself is
 * enforced by the API, and an expired/revoked one produces a 401 that the
 * React Query client turns into a sign-out and a redirect back here.
 *
 * 1. Unauthenticated users are redirected to /login (with the return path).
 * 2. Authenticated users are bounced away from /login to the main app.
 * 3. /api is passed through: the API answers 401/403 itself (login included).
 */
export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const isLoginRoute = pathname === "/login";
  const token = request.cookies.get(SESSION_COOKIE_NAME)?.value;

  if (pathname.startsWith("/api")) {
    return NextResponse.next();
  }

  // Unauthenticated: redirect to /login, preserving the intended destination
  if (!token && !isLoginRoute) {
    const loginUrl = request.nextUrl.clone();
    loginUrl.pathname = "/login";
    loginUrl.search = ""; // clear current route's params before setting redirectTo
    loginUrl.searchParams.set("redirectTo", pathname + request.nextUrl.search);
    return NextResponse.redirect(loginUrl);
  }

  // Authenticated: bounce away from /login to the main app
  if (token && isLoginRoute) {
    const homeUrl = request.nextUrl.clone();
    homeUrl.pathname = "/";
    homeUrl.search = "";
    return NextResponse.redirect(homeUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths except for:
     * - _next/static (JS/CSS bundles)
     * - _next/image (image optimisation)
     * - favicon.ico
     * - Public assets (images, SVGs, etc.)
     */
    "/((?!_next/static|_next/image|favicon\\.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)",
  ],
};
