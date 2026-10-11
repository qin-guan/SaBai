/**
 * Helpers for reading errors thrown by @sabai/sdk.
 *
 * The SDK throws its own `ResponseError` class which is not exported from the
 * public subpaths, so these helpers duck-type on the shape instead of using
 * `instanceof`.
 */

interface ApiErrorLike {
  status?: number;
  data?: unknown;
  message?: string;
}

export function getApiErrorStatus(error: unknown): number | undefined {
  if (error && typeof error === "object" && "status" in error) {
    const { status } = error as ApiErrorLike;
    if (typeof status === "number") return status;
  }
  return undefined;
}

export function isUnauthorizedError(error: unknown): boolean {
  return getApiErrorStatus(error) === 401;
}

/**
 * Extracts a human-readable message from an API error. The API returns
 * RFC 7807 ProblemDetails, so prefer `detail`/`title`, then the first field
 * validation error.
 */
export function getApiErrorMessage(
  error: unknown,
  fallback = "Something went wrong.",
): string {
  if (!error || typeof error !== "object") return fallback;

  const { data, message } = error as ApiErrorLike;

  if (data && typeof data === "object") {
    const problem = data as {
      detail?: string;
      title?: string;
      errors?: Record<string, string[]>;
    };

    if (problem.detail) return problem.detail;
    if (problem.title) return problem.title;
    if (problem.errors) {
      const first = Object.values(problem.errors)[0];
      if (first?.[0]) return first[0];
    }
  }

  return message ?? fallback;
}
