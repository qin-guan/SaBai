import { NextResponse, type NextRequest } from "next/server";
import { TOKEN_COOKIE_NAME } from "@/lib/api/token";

/**
 * Route protection for the web app.
 *
 * The app authenticates against SaBai.API with a JWT bearer token stored in the
 * `sabai_token` cookie. This middleware only checks that the cookie is present:
 * token validity is enforced by the API, and an expired/invalid token produces a
 * 401 that the React Query client turns into a redirect back here.
 *
 * 1. Unauthenticated users are redirected to /login (with the return path).
 * 2. Authenticated users are bounced away from /login to the main app.
 */
export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const isLoginRoute = pathname === "/login";
  const token = request.cookies.get(TOKEN_COOKIE_NAME)?.value;

  // Allow unauthenticated access to API routes in development so the API can
  // return 401/403 as appropriate (e.g. Postman / integration testing).
  const isApiRoute = pathname.startsWith("/api");
  const allowAPILogin = process.env.NODE_ENV === "development" && isApiRoute;

  // Unauthenticated: redirect to /login, preserving the intended destination
  if (!token && !isLoginRoute && !allowAPILogin) {
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
