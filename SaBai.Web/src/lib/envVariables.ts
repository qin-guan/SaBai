import { z } from "zod";

/*
 * Consolidated environment variable validation for both client and server contexts.
 *
 * On the server, all variables are validated. On the client, only NEXT_PUBLIC_
 * variables are included in the schema — server vars are excluded entirely,
 * so validation never attempts to access them in the browser.
 *
 * The web app is now a pure client of SaBai.API (through @sabai/sdk); the only
 * variable it needs is the API base URL. All database, storage and face
 * recognition work lives in the API.
 */

const isServer = typeof window === "undefined";

const clientSchema = z.object({
  NEXT_PUBLIC_API_URL: z.url(),
});

const serverSchema = clientSchema.extend({
  NODE_ENV: z
    .enum(["development", "production", "test"])
    .default("development"),
});

/*
 * NEXT_PUBLIC_ vars must be passed explicitly (not via process.env spread) because
 * Next.js only statically inlines them as string replacements at build time —
 * spreading process.env on the client yields an empty object.
 */
const input = isServer
  ? process.env
  : {
      NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
    };

const parsed = isServer
  ? serverSchema.safeParse(input)
  : clientSchema.safeParse(input);

if (!parsed.success) {
  const issues = parsed.error.issues.map(
    (issue) => `${issue.path.join(".")}: ${issue.message}`,
  );

  throw new Error(
    `Missing or invalid environment variables:\n${issues.map((value) => `- ${value}`).join("\n")}`,
  );
}

const envVariables = parsed.data as z.infer<typeof serverSchema>;

export default envVariables;
