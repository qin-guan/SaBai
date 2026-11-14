import type { QueryClient } from '@tanstack/vue-query'
import {
  saBaiAPIEndpointsVillageCodesCreatePostEndpoint,
  saBaiAPIEndpointsVillageCodesDeleteDeleteEndpoint,
  saBaiAPIEndpointsVillageCodesListGetEndpoint,
  saBaiAPIEndpointsVillageCodesUpdatePatchEndpoint,
} from '@sabai/sdk/clients'
import type {
  SaBaiAPIEndpointsVillageCodesCreatePostRequest,
  SaBaiAPIEndpointsVillageCodesListGetItem,
  SaBaiAPIEndpointsVillageCodesListGetResponse,
} from '@sabai/sdk/types'
import { defineOfflineMutation, requestOptions } from '~/offline/defineOfflineMutation'
import { registerOfflinePrefetch } from '~/offline/prefetch'
import { apiConfig, queryKeys } from '~/utils/api'
import { getApiErrorStatus } from '~/utils/apiErrors'

/**
 * Village codes: reads, offline-capable writes and offline download.
 * This is the reference for how a domain plugs into the offline layer:
 * 1. one fetch function per query, used by both the composable and the prefetch;
 * 2. pure, idempotent cache edits by id (`upsert…`/`remove…`) shared by
 *    `optimistic` and `onSuccess`;
 * 3. one `defineOfflineMutation` per REST write, with everything `send` needs in `vars`;
 * 4. a `registerOfflinePrefetch` for what a device needs offline.
 */

export type VillageCode = SaBaiAPIEndpointsVillageCodesListGetItem

export interface VillageCodeFields {
  code: string
  name: string
  colorHex: string
  isVisible: boolean
}

export interface SaveVillageCodeVars extends VillageCodeFields {
  id: string
}

export interface DeleteVillageCodeVars {
  id: string
  /** Shown in the pending-changes list. */
  code: string
  name: string
}

// ── Reads ────────────────────────────────────────────────────────────────

export function fetchVillageCodes(includeHidden: boolean, signal?: AbortSignal) {
  return saBaiAPIEndpointsVillageCodesListGetEndpoint({ ...apiConfig, query: { includeHidden }, signal }).unwrap()
}

registerOfflinePrefetch('village-codes', async (queryClient) => {
  await Promise.all([false, true].map(includeHidden =>
    queryClient.fetchQuery({
      queryKey: queryKeys.villageCodesList(includeHidden),
      queryFn: ({ signal }) => fetchVillageCodes(includeHidden, signal),
    }),
  ))
})

// ── Cache edits ──────────────────────────────────────────────────────────

type VillageCodesList = SaBaiAPIEndpointsVillageCodesListGetResponse

/**
 * Puts `village` into both cached lists (all codes, and visible codes when it
 * is visible), replacing an entry with the same id or adding it first (the
 * API lists newest first). Lists that are not cached are left alone.
 */
function upsertVillageCode(queryClient: QueryClient, village: VillageCode) {
  for (const includeHidden of [true, false]) {
    queryClient.setQueryData<VillageCodesList>(queryKeys.villageCodesList(includeHidden), (old) => {
      if (!old) return old
      const belongs = includeHidden || village.isVisible
      const others = old.villageCodes.filter(v => v.id !== village.id)
      if (!belongs) return { ...old, villageCodes: others }
      const index = old.villageCodes.findIndex(v => v.id === village.id)
      const villageCodes = index === -1
        ? [village, ...others]
        : old.villageCodes.map(v => (v.id === village.id ? village : v))
      return { ...old, villageCodes }
    })
  }
}

function removeVillageCode(queryClient: QueryClient, id: string) {
  for (const includeHidden of [true, false]) {
    queryClient.setQueryData<VillageCodesList>(queryKeys.villageCodesList(includeHidden), old =>
      old && { ...old, villageCodes: old.villageCodes.filter(v => v.id !== id) })
  }
}

/** The cached record, if any list has it (keeps `createdAt` for an edit). */
function findCached(queryClient: QueryClient, id: string): VillageCode | undefined {
  for (const includeHidden of [true, false]) {
    const found = queryClient.getQueryData<VillageCodesList>(queryKeys.villageCodesList(includeHidden))
      ?.villageCodes.find(v => v.id === id)
    if (found) return found
  }
  return undefined
}

function optimisticRecord(queryClient: QueryClient, vars: SaveVillageCodeVars): VillageCode {
  const now = new Date().toISOString()
  const cached = findCached(queryClient, vars.id)
  return {
    createdAt: now,
    ...cached,
    id: vars.id,
    code: vars.code,
    name: vars.name,
    colorHex: vars.colorHex,
    isVisible: vars.isVisible,
    updatedAt: now,
  }
}

const afterSave = async (saved: VillageCode, _vars: unknown, queryClient: QueryClient) => {
  upsertVillageCode(queryClient, saved)
  await queryClient.invalidateQueries({ queryKey: queryKeys.villageCodes() })
}

// ── Writes ───────────────────────────────────────────────────────────────

export const createVillageCode = defineOfflineMutation({
  key: 'villageCodes.create',
  describe: (vars: SaveVillageCodeVars) => `New village code ${vars.code} (${vars.name})`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) => {
    // `id` is the client-chosen id the create endpoint accepts (idempotent on
    // replay). Typed as an intersection so it compiles with an SDK generated
    // before the API gained the field; drop the `& { id }` once it has it.
    const body: SaBaiAPIEndpointsVillageCodesCreatePostRequest & { id: string } = {
      id: vars.id,
      code: vars.code,
      name: vars.name,
      colorHex: vars.colorHex,
      isVisible: vars.isVisible,
    }
    return saBaiAPIEndpointsVillageCodesCreatePostEndpoint({ ...apiConfig, ...requestOptions(ctx), body }).unwrap()
  },
  optimistic: (vars, queryClient) => upsertVillageCode(queryClient, optimisticRecord(queryClient, vars)),
  onSuccess: afterSave,
})

export const updateVillageCode = defineOfflineMutation({
  key: 'villageCodes.update',
  describe: (vars: SaveVillageCodeVars) => `Edit village code ${vars.code} (${vars.name})`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsVillageCodesUpdatePatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: { code: vars.code, name: vars.name, colorHex: vars.colorHex, isVisible: vars.isVisible },
    }).unwrap(),
  optimistic: (vars, queryClient) => upsertVillageCode(queryClient, optimisticRecord(queryClient, vars)),
  onSuccess: afterSave,
})

export const deleteVillageCode = defineOfflineMutation({
  key: 'villageCodes.delete',
  describe: (vars: DeleteVillageCodeVars) => `Delete village code ${vars.code} (${vars.name})`,
  recordIds: vars => [vars.id],
  send: async (vars, ctx) => {
    try {
      await saBaiAPIEndpointsVillageCodesDeleteDeleteEndpoint({ ...apiConfig, ...requestOptions(ctx), path: { id: vars.id } }).unwrap()
    }
    catch (error) {
      // Already gone (e.g. a replay of a delete whose response was lost): done.
      if (getApiErrorStatus(error) !== 404) throw error
    }
  },
  optimistic: (vars, queryClient) => removeVillageCode(queryClient, vars.id),
  onSuccess: (_result, _vars, queryClient) => queryClient.invalidateQueries({ queryKey: queryKeys.villageCodes() }),
})
