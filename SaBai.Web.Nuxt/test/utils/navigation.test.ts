import type { RouteLocationNormalizedLoaded } from 'vue-router'
import type { NavItem } from '~/utils/navigation'
import { ADMIN_NAV_ITEMS, ALL_NAV_ITEMS, CHANGELOG_PAGE, currentStation, isInNavSection, isNavItemActive, logicalBackTarget, MEDICATION_PAGES, NAV_GROUPS, navLabelOf, PATIENT_NAV_ITEMS, patientTargetFor, PHARMACY_NAV_ITEMS, PHARMACY_STATION_PAGE, REFERRAL_NAV_ITEMS, SUB_PAGES } from '~/utils/navigation'

type Route = Pick<RouteLocationNormalizedLoaded, 'path' | 'query' | 'meta'>

function route(path: string, query: Route['query'] = {}, meta: Route['meta'] = {}): Route {
  return { path, query, meta }
}

describe('logicalBackTarget', () => {
  it('goes from patient details to the patient list', () => {
    expect(logicalBackTarget(route('/patient/42'))).toBe('/patient')
  })

  it('goes from a station form to that station worklist', () => {
    expect(logicalBackTarget(route('/vitals/42', {}, { patientLink: 'vitals' }))).toBe('/patient?station=vitals')
    expect(logicalBackTarget(route('/vision/update-glasses/42', {}, { patientLink: 'vision' }))).toBe('/patient?station=vision')
    expect(logicalBackTarget(route('/consults/42', {}, { patientLink: 'consults' }))).toBe('/patient?station=consults')
    expect(logicalBackTarget(route('/dental/42', {}, { patientLink: 'dental' }))).toBe('/patient?station=dental')
  })

  it('goes from a station-filtered patient list to all patients', () => {
    expect(logicalBackTarget(route('/patient', { station: 'vitals' }))).toBe('/patient')
    expect(logicalBackTarget(route('/patient', { station: 'pharmacy' }))).toBe('/patient')
  })

  it('goes from medication sub-pages to medication stock', () => {
    expect(logicalBackTarget(route('/medication-brands'))).toBe('/medication-stock')
    expect(logicalBackTarget(route('/medication-active-ingredients'))).toBe('/medication-stock')
  })

  it('goes from settings sub-pages to settings', () => {
    expect(logicalBackTarget(route('/settings/village-codes'))).toBe('/settings')
    expect(logicalBackTarget(route('/settings/notifications'))).toBe('/settings')
    expect(logicalBackTarget(route('/settings/users'))).toBe('/settings')
    expect(logicalBackTarget(route('/settings/audit-log', { entityType: 'Patient', entityId: '42' }))).toBe('/settings')
  })

  it('goes from referral details to the referrals list', () => {
    expect(logicalBackTarget(route('/referrals/42'))).toBe('/referrals')
  })

  it('returns undefined on top-level pages', () => {
    expect(logicalBackTarget(route('/patient'))).toBeUndefined()
    expect(logicalBackTarget(route('/scan-face'))).toBeUndefined()
    expect(logicalBackTarget(route('/referrals'))).toBeUndefined()
    expect(logicalBackTarget(route('/medication-stock'))).toBeUndefined()
    expect(logicalBackTarget(route('/orders'))).toBeUndefined()
    expect(logicalBackTarget(route('/settings'))).toBeUndefined()
    expect(logicalBackTarget(route('/account'))).toBeUndefined()
    expect(logicalBackTarget(route('/changelog'))).toBeUndefined()
  })
})

describe('currentStation and patientTargetFor', () => {
  it('detects the active station from route meta or query', () => {
    expect(currentStation(route('/vitals/42', {}, { patientLink: 'vitals' }))).toBe('vitals')
    expect(currentStation(route('/patient', { station: 'dental' }))).toBe('dental')
    expect(currentStation(route('/patient'))).toBeUndefined()
  })

  it('targets the patient record when not in a per-patient station', () => {
    expect(patientTargetFor(route('/patient'))).toBe('patient')
    expect(patientTargetFor(route('/patient', { station: 'pharmacy' }))).toBe('patient')
    expect(patientTargetFor(route('/patient', { station: 'vision' }))).toBe('vision')
  })
})

describe('paths.consultsDetail', () => {
  it('builds consults detail URLs with optional visit and consult query params', async () => {
    const { paths } = await import('~/utils/paths')
    expect(paths.consultsDetail(42)).toBe('/consults/42')
    expect(paths.consultsDetail(42, 7)).toBe('/consults/42?visit=7')
    expect(paths.consultsDetail(42, undefined, 15)).toBe('/consults/42?consult=15')
    expect(paths.consultsDetail(42, 7, 15)).toBe('/consults/42?visit=7&consult=15')
  })
})

describe('paths.orders and pharmacy patientLinkFor', () => {
  it('builds orders URLs and pharmacy patient links correctly', async () => {
    const { paths, patientLinkFor } = await import('~/utils/paths')
    expect(paths.orders()).toBe('/orders')
    expect(paths.orders({ patient: 'p1', visit: 'v1' })).toBe('/orders?patient=p1&visit=v1')
    expect(patientLinkFor('pharmacy', 'p1', 'v1')).toBe('/orders?patient=p1&visit=v1')
  })
})

describe('sidebar', () => {
  const labels = (items: NavItem[]) => items.map(navLabelOf)

  it('groups the entries as Patients, Stations, Pharmacy, Referrals and Admin', () => {
    expect(NAV_GROUPS.map(group => [group.label, labels(group.items)])).toEqual([
      ['Patients', ['Scan face', 'Patient list']],
      ['Stations', ['Vitals', 'Vision', 'Consult', 'Dental']],
      ['Pharmacy', ['Stock', 'Orders']],
      ['Referrals', ['Referral list', 'PN bills']],
      ['Admin', ['Settings']],
    ])
    expect(ALL_NAV_ITEMS).toEqual(NAV_GROUPS.flatMap(group => group.items))
  })

  it('links Stock and Orders to their pages', () => {
    expect(PHARMACY_NAV_ITEMS.map(item => item.href)).toEqual(['/medication-stock', '/orders'])
  })

  it('has no pharmacy station entry, but keeps the page searchable', () => {
    expect(ALL_NAV_ITEMS.some(item => item.station === 'pharmacy')).toBe(false)
    expect(SUB_PAGES).toContain(PHARMACY_STATION_PAGE)
    expect(PHARMACY_STATION_PAGE.href).toBe('/patient?station=pharmacy')
    expect(isNavItemActive(PHARMACY_STATION_PAGE, route('/patient', { station: 'pharmacy' }))).toBe(true)
  })

  it('lists every page once for search', () => {
    const hrefs = [...ALL_NAV_ITEMS, ...SUB_PAGES].map(item => item.href)
    expect(new Set(hrefs).size).toBe(hrefs.length)
  })

  it('shows Stock as active on the stock, brands and active ingredients routes', () => {
    const stock = PHARMACY_NAV_ITEMS[0]!
    expect(stock.children).toBeUndefined()
    for (const path of ['/medication-stock', '/medication-brands', '/medication-active-ingredients'])
      expect(isInNavSection(stock, route(path))).toBe(true)
    expect(isInNavSection(stock, route('/orders'))).toBe(false)
    for (const page of MEDICATION_PAGES)
      expect(SUB_PAGES).toContain(page)
  })

  it('marks one referral entry at a time', () => {
    const [list, bills] = REFERRAL_NAV_ITEMS as [NavItem, NavItem]
    expect(isInNavSection(list, route('/referrals'))).toBe(true)
    expect(isInNavSection(list, route('/referrals/42'))).toBe(true)
    expect(isInNavSection(list, route('/referrals/bills'))).toBe(false)
    expect(isInNavSection(bills, route('/referrals/bills'))).toBe(true)
    expect(isInNavSection(bills, route('/referrals'))).toBe(false)
  })

  it('marks the patient list only when no station is chosen', () => {
    const list = PATIENT_NAV_ITEMS[1]!
    expect(isInNavSection(list, route('/patient'))).toBe(true)
    expect(isInNavSection(list, route('/patient/42'))).toBe(true)
    expect(isInNavSection(list, route('/patient', { station: 'pharmacy' }))).toBe(false)
  })
})

describe('nested pages', () => {
  const settings = ADMIN_NAV_ITEMS.find(item => item.href === '/settings')!

  it('counts a nested page as part of its parent section', () => {
    expect(isInNavSection(settings, route('/settings/audit-log'))).toBe(true)
    expect(isInNavSection(settings, route('/settings'))).toBe(true)
    expect(isInNavSection(settings, route('/orders'))).toBe(false)
  })

  it('marks the nested page itself, ignoring audit log filters', () => {
    const auditLog = settings.children!.find(item => item.name === 'Audit Log')!
    expect(isNavItemActive(auditLog, route('/settings/audit-log', { entityType: 'Patient' }))).toBe(true)
    expect(isNavItemActive(auditLog, route('/settings/users'))).toBe(false)
  })

  it('makes every nested page searchable', () => {
    for (const child of settings.children!)
      expect(SUB_PAGES).toContain(child)
    expect(SUB_PAGES).toContain(CHANGELOG_PAGE)
  })
})
