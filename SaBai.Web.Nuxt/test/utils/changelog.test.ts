import { CHANGELOG_CATEGORY_META, CHANGELOG_DAYS } from '~/utils/changelog'

describe('changelog data', () => {
  it('covers the past 7 days in reverse chronological order', () => {
    expect(CHANGELOG_DAYS).toHaveLength(7)
    expect(CHANGELOG_DAYS[0]?.date).toBe('2026-11-16')
    expect(CHANGELOG_DAYS[CHANGELOG_DAYS.length - 1]?.date).toBe('2026-11-10')

    for (let i = 1; i < CHANGELOG_DAYS.length; i++) {
      expect(CHANGELOG_DAYS[i - 1]!.date > CHANGELOG_DAYS[i]!.date).toBe(true)
    }
  })

  it('has valid entries with supported categories', () => {
    for (const day of CHANGELOG_DAYS) {
      expect(day.label.trim().length).toBeGreaterThan(0)
      expect(day.summary.trim().length).toBeGreaterThan(0)
      expect(day.entries.length).toBeGreaterThan(0)

      for (const entry of day.entries) {
        expect(entry.title.trim().length).toBeGreaterThan(0)
        expect(entry.description.trim().length).toBeGreaterThan(0)
        expect(CHANGELOG_CATEGORY_META[entry.category]).toBeDefined()
      }
    }
  })
})
