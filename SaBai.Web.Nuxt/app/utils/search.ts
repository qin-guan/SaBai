import MiniSearch from 'minisearch'
import type { SearchResult } from 'minisearch'
import { formatPatientCode, formatPatientId } from './patient'

/**
 * Indexes for the universal search box (header / Ctrl+K).
 *
 * Patients are matched on name, every form of their ID ("12", "0012",
 * "PC0012"), identification number, phone and village; sections on their
 * name plus clinical keywords ("bp" → Vitals, "glasses" → Vision).
 * Prefix matching makes partial input work; light fuzziness forgives typos.
 */

export interface SearchablePatient {
  id: number
  name: string
  identificationNumber?: string | null
  contactNo?: string | null
  villageCode?: string | null
  displayId?: string | null
}

export interface SearchablePage {
  id: string
  title: string
  keywords: string
}

const SEARCH_OPTIONS = {
  prefix: true,
  // Typo tolerance only for words (names, sections) of 4+ letters — never for
  // IDs or numbers, where "0004" must not match "0007".
  fuzzy: (term: string) => (/^\p{L}{4,}$/u.test(term) ? 0.2 : false),
  combineWith: 'AND' as const,
}

export function buildPatientIndex(patients: SearchablePatient[]) {
  const index = new MiniSearch<SearchablePatient & { ids: string, digits: string }>({
    fields: ['name', 'ids', 'identificationNumber', 'digits', 'villageCode'],
    storeFields: ['id'],
    searchOptions: { ...SEARCH_OPTIONS, boost: { ids: 3, name: 2 } },
  })
  index.addAll(
    patients.map(p => ({
      ...p,
      ids: [String(p.id), formatPatientId(p.id), formatPatientCode(p.villageCode, p.id), p.displayId ?? ''].join(' '),
      // Phone numbers are typed without spaces/dashes.
      digits: (p.contactNo ?? '').replace(/\D/g, ''),
    })),
  )
  return index
}

export function buildPageIndex(pages: SearchablePage[]) {
  const index = new MiniSearch<SearchablePage>({
    fields: ['title', 'keywords'],
    storeFields: ['id'],
    searchOptions: { ...SEARCH_OPTIONS, combineWith: 'OR', boost: { title: 3 } },
  })
  index.addAll(pages)
  return index
}

/** Runs `query` against `index`; digits-only queries also match phone numbers without separators. */
export function searchIndex(index: MiniSearch, query: string): SearchResult[] {
  const q = query.trim()
  if (!q)
    return []
  return index.search(q)
}
