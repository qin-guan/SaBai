import env from "@/lib/envVariables";
import { getToken } from "./token";

/**
 * Shared configuration passed to every @sabai/sdk call (queries and mutations).
 *
 * The generated SDK does not export a configurable client instance, so the
 * `baseURL` and bearer `auth` resolver are supplied per call through the
 * `config` argument of the generated `*QueryOptions` / `*MutationOptions`
 * helpers.
 */
export const apiConfig = {
  baseURL: env.NEXT_PUBLIC_API_URL,
  auth: () => getToken(),
};
