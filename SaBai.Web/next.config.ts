import type { NextConfig } from "next";
import path from "node:path";
import { fileURLToPath } from "node:url";

const appDir = path.dirname(fileURLToPath(import.meta.url));

// Server-side URL of SaBai.API. Read when the server starts (dev) or at build
// time (production build), so set it in both places.
const apiUrl = (process.env.API_URL ?? "http://localhost:5250").replace(/\/+$/, "");

const nextConfig: NextConfig = {
  reactStrictMode: true,
  output: "standalone",
  outputFileTracingRoot: path.join(appDir, ".."),
  // The browser calls the API on this origin under /api, so the API's HttpOnly
  // session cookie is first-party (no CORS, no third-party cookies).
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl}/:path*` }];
  },
};

export default nextConfig;
