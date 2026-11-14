import { uuidv7 } from '~/utils/uuid'

/** All known freeform tags across staff users and patients, for autocomplete. */
export function useTags() {
  return useLocalQuery<string[]>(
    ['tags', 'patient_tags'],
    async () => {
      const db = getPowerSyncInstance()
      const rows = await db.getAll<{ name: string }>(
        'SELECT name FROM tags WHERE name IS NOT NULL ORDER BY name ASC',
      )
      return rows.map(r => r.name)
    },
  )
}

/**
 * Syncs a patient's freeform tags in local SQLite (`tags` + `patient_tags`).
 * Reuses existing tags by case-insensitive `normalized_name` and carries `tag_name`
 * on `patient_tags` so the server can reconcile concurrent offline tag creations.
 */
export async function syncLocalPatientTags(patientId: string, rawTags: readonly string[] | undefined) {
  const db = getPowerSyncInstance()
  const seen = new Set<string>()
  const names: string[] = []
  for (const raw of rawTags ?? []) {
    const trimmed = raw?.trim()
    if (!trimmed)
      continue
    const key = trimmed.toUpperCase()
    if (!seen.has(key)) {
      seen.add(key)
      names.push(trimmed)
    }
  }

  const now = new Date().toISOString()
  const resolved = new Map<string, { id: string, name: string }>()
  for (const name of names) {
    const norm = name.toUpperCase()
    const existing = await db.getOptional<{ id: string, name: string }>(
      'SELECT id, name FROM tags WHERE normalized_name = ? LIMIT 1',
      [norm],
    )
    if (existing) {
      resolved.set(existing.id, existing)
    }
    else {
      const tagId = uuidv7()
      await db.execute(
        'INSERT INTO tags (id, name, normalized_name, created_at) VALUES (?, ?, ?, ?)',
        [tagId, name, norm, now],
      )
      resolved.set(tagId, { id: tagId, name })
    }
  }

  const existingLinks = await db.getAll<{ id: string, tag_id: string }>(
    'SELECT id, tag_id FROM patient_tags WHERE patient_id = ?',
    [patientId],
  )
  const existingTagIds = new Set<string>()
  for (const link of existingLinks) {
    if (!resolved.has(link.tag_id)) {
      await db.execute('DELETE FROM patient_tags WHERE id = ?', [link.id])
    }
    else {
      existingTagIds.add(link.tag_id)
    }
  }

  for (const [tagId, tag] of resolved) {
    if (!existingTagIds.has(tagId)) {
      await db.execute(
        'INSERT INTO patient_tags (id, patient_id, tag_id, tag_name) VALUES (?, ?, ?, ?)',
        [uuidv7(), patientId, tagId, tag.name],
      )
    }
  }
}
