import MiniSearch from 'minisearch'
import type { SearchResult } from 'minisearch'
import {
  cleanKhmerText,
  expandKhmerSearchTerms,
  hasKhmerScript,
  normalizeKhmerDigits,
  segmentKhmerWords,
} from './khmer'
import { formatPatientCode, formatPatientId } from './patient'

/**
 * Indexes for the universal search box (header / Ctrl+K).
 *
 * Patients are matched on name (including Khmer names with or without spaces
 * or zero-width spaces), every form of their ID ("12", "0012", "PC0012"),
 * identification number, phone and village; sections on their name plus
 * clinical keywords ("bp" → Vitals, "glasses" → Vision).
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

function tokenizeSearchText(text: string): string[] {
  return normalizeKhmerDigits(cleanKhmerText(text))
    .split(/[\n\r\p{Z}\p{P}\u200B\uFEFF]+/u)
    .filter(Boolean)
}

const SEARCH_OPTIONS = {
  prefix: true,
  // Typo tolerance only for words (names, sections) of 4+ letters/marks — never
  // for IDs or numbers, where "0004" must not match "0007". Including \p{M}
  // covers Khmer dependent vowels, subscript signs (coeng) and diacritics.
  fuzzy: (term: string) => (/^[\p{L}\p{M}]{4,}$/u.test(term) ? 0.2 : false),
  combineWith: 'AND' as const,
}

export function buildPatientIndex(patients: SearchablePatient[]) {
  const index = new MiniSearch<SearchablePatient & { ids: string, digits: string }>({
    fields: ['name', 'ids', 'identificationNumber', 'digits', 'villageCode'],
    storeFields: ['id'],
    tokenize: tokenizeSearchText,
    searchOptions: { ...SEARCH_OPTIONS, tokenize: tokenizeSearchText, boost: { ids: 3, name: 2 } },
  })
  index.addAll(
    patients.map(p => ({
      ...p,
      name: expandKhmerSearchTerms(p.name),
      identificationNumber: normalizeKhmerDigits(cleanKhmerText(p.identificationNumber)),
      villageCode: normalizeKhmerDigits(cleanKhmerText(p.villageCode)),
      ids: [String(p.id), formatPatientId(p.id), formatPatientCode(p.villageCode, p.id), p.displayId ?? ''].join(' '),
      // Phone numbers are typed without spaces/dashes (and may use Khmer numerals).
      digits: normalizeKhmerDigits(p.contactNo ?? '').replace(/\D/g, ''),
    })),
  )
  return index
}

export function buildPageIndex(pages: SearchablePage[]) {
  const index = new MiniSearch<SearchablePage>({
    fields: ['title', 'keywords'],
    storeFields: ['id'],
    tokenize: tokenizeSearchText,
    searchOptions: { ...SEARCH_OPTIONS, tokenize: tokenizeSearchText, combineWith: 'OR', boost: { title: 3 } },
  })
  index.addAll(
    pages.map(p => ({
      ...p,
      title: expandKhmerSearchTerms(p.title),
      keywords: expandKhmerSearchTerms(p.keywords),
    })),
  )
  return index
}

/** Runs `query` against `index`; digits-only queries (ASCII or Khmer) also match phone numbers without separators. */
export function searchIndex(index: MiniSearch, query: string): SearchResult[] {
  const q = normalizeKhmerDigits(cleanKhmerText(query))
  if (!q)
    return []
  const results = index.search(q)
  if (results.length > 0 || !hasKhmerScript(q))
    return results

  const segmented = segmentKhmerWords(q)
  if (segmented.length > 1)
    return index.search(segmented.join(' '))
  return results
}

