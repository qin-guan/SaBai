const KHMER_DIGIT_ZERO = 0x17E0
const KHMER_DIGIT_RE = /[\u17E0-\u17E9]/g
const ZERO_WIDTH_SPACE_RE = /[\u200B\uFEFF]+/g
const EDGE_INVISIBLE_RE = /^[\s\u200B\u200C\u200D\uFEFF]+|[\s\u200B\u200C\u200D\uFEFF]+$/g
const KHMER_SCRIPT_RE = /\p{Script=Khmer}/u
const KHMER_RUN_RE = /[\p{Script=Khmer}\u200B\u200C\u200D]+/gu

const khmerWordSegmenter
  = typeof Intl !== 'undefined' && typeof Intl.Segmenter === 'function'
    ? new Intl.Segmenter('km', { granularity: 'word' })
    : null

/** Converts Khmer numerals (០–៩, U+17E0–U+17E9) to ASCII digits (0–9). */
export function normalizeKhmerDigits(value: string): string {
  return value.replace(KHMER_DIGIT_RE, ch => String(ch.charCodeAt(0) - KHMER_DIGIT_ZERO))
}

/**
 * Normalizes text to NFC, converts zero-width spaces (U+200B, U+FEFF) used as
 * word breaks on Khmer keyboards into standard spaces, collapses repeated
 * whitespace, and trims leading/trailing whitespace and invisible joiners.
 */
export function cleanKhmerText(value: string | null | undefined): string {
  if (!value)
    return ''
  return value
    .normalize('NFC')
    .replace(ZERO_WIDTH_SPACE_RE, ' ')
    .replace(EDGE_INVISIBLE_RE, '')
    .replace(/\s+/g, ' ')
}

/** Whether `value` contains any characters in the Khmer Unicode script. */
export function hasKhmerScript(value: string): boolean {
  return KHMER_SCRIPT_RE.test(value)
}

/**
 * Segments text into words using `Intl.Segmenter('km', { granularity: 'word' })`,
 * falling back to whitespace/punctuation splitting if `Intl.Segmenter` is unavailable.
 */
export function segmentKhmerWords(value: string): string[] {
  const cleaned = normalizeKhmerDigits(cleanKhmerText(value))
  if (!cleaned)
    return []
  if (!khmerWordSegmenter)
    return cleaned.split(/[\n\r\p{Z}\p{P}\u200B\uFEFF]+/u).filter(Boolean)

  const words: string[] = []
  for (const part of khmerWordSegmenter.segment(cleaned)) {
    if (part.isWordLike) {
      const token = part.segment.replace(EDGE_INVISIBLE_RE, '')
      if (token)
        words.push(token)
    }
  }
  return words
}

/**
 * Expands a string containing Khmer script into searchable tokens so that names
 * and phrases match regardless of whether the stored record or the search query
 * uses spaces, zero-width spaces, or no spaces between Khmer words.
 *
 * For example, both "សុខ តារា" and "សុខតារា" expand to include "សុខ", "តារា",
 * and "សុខតារា".
 */
export function expandKhmerSearchTerms(value: string | null | undefined): string {
  const cleaned = normalizeKhmerDigits(cleanKhmerText(value))
  if (!cleaned)
    return ''
  if (!hasKhmerScript(cleaned))
    return cleaned

  const tokens = new Set<string>()
  for (const rawToken of cleaned.split(/[\n\r\p{Z}\p{P}]+/u)) {
    if (rawToken)
      tokens.add(rawToken)
  }

  for (const word of segmentKhmerWords(cleaned))
    tokens.add(word)

  // Also index contiguous Khmer word suffixes across spaces (e.g. "សុខ តារា" → "សុខតារា", "តារា")
  // so unspaced prefix queries like "សុខតា" match spaced names and vice versa.
  const khmerRuns = cleaned.match(/[\p{Script=Khmer}\s\u200C\u200D]+/gu) ?? []
  for (const run of khmerRuns) {
    const words = segmentKhmerWords(run).filter(hasKhmerScript)
    for (let i = 0; i < words.length; i++) {
      tokens.add(words.slice(i).join(''))
    }
    const collapsed = run.replace(/\s+/g, '')
    if (collapsed)
      tokens.add(collapsed)
  }

  // Strip any inner zero-width characters from Khmer runs as well.
  for (const match of cleaned.matchAll(KHMER_RUN_RE)) {
    const stripped = match[0].replace(/[\u200B\u200C\u200D]/g, '')
    if (stripped)
      tokens.add(stripped)
  }

  return [...tokens].join(' ')
}

/**
 * Normalizes a string for case-insensitive, Khmer-aware substring comparison
 * (NFC, Khmer digits → ASCII digits, zero-width spaces → spaces, lowercased).
 */
export function normalizeForSearch(value: string | null | undefined): string {
  return normalizeKhmerDigits(cleanKhmerText(value)).toLowerCase()
}

/**
 * Checks whether `haystack` matches a free-text `query`, handling Khmer numerals,
 * zero-width spaces, and Khmer names written with or without spaces.
 */
export function matchesSearchText(haystack: string | null | undefined, query: string): boolean {
  const needle = normalizeForSearch(query)
  if (!needle)
    return false
  const target = normalizeForSearch(haystack)
  if (!target)
    return false
  if (target.includes(needle))
    return true

  if (hasKhmerScript(target) || hasKhmerScript(needle)) {
    const collapsedTarget = target.replace(/[\s\u200C\u200D]+/g, '')
    const collapsedNeedle = needle.replace(/[\s\u200C\u200D]+/g, '')
    if (collapsedNeedle && collapsedTarget.includes(collapsedNeedle))
      return true

    const queryWords = needle.split(/\s+/).filter(Boolean)
    if (queryWords.length > 1 && queryWords.every(w => target.includes(w) || collapsedTarget.includes(w)))
      return true
  }

  return false
}
