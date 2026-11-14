import type { QueryClient } from '@tanstack/vue-query'
import { readLocal, writeLocal } from './storage'

/**
 * Which staff user the cached query data on this device belongs to.
 *
 * The persisted cache holds what one user fetched (and their optimistic
 * edits). When someone else signs in on the same device — typically after the
 * first user's session expired without signing out — that cache is dropped:
 * it may hold data the new user's role cannot read, and edits they did not
 * make. Their queued writes are kept separately in the outbox (parked).
 */

export const CACHE_OWNER_KEY = 'sabai.offline.cacheOwner'

/**
 * Records `userId` as the owner of the cache. Unless they already owned it,
 * removes every cached query except those matching `keep` (the session query)
 * — the persister then saves the emptied cache — and returns true. A cache
 * with no recorded owner is dropped too: whose it was cannot be known.
 */
export function claimQueryCache(
  queryClient: QueryClient,
  userId: string,
  keep: (queryKey: readonly unknown[]) => boolean = () => false,
): boolean {
  const owner = readLocal<string>(CACHE_OWNER_KEY)
  if (owner === userId) return false
  writeLocal(CACHE_OWNER_KEY, userId)
  queryClient.removeQueries({ predicate: query => !keep(query.queryKey) })
  return true
}

/** Forgets the owner (sign-out, after the cache was cleared). */
export function releaseQueryCache(): void {
  writeLocal(CACHE_OWNER_KEY, undefined)
}
