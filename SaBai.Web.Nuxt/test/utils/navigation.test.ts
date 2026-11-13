import type { RouteLocationNormalizedLoaded } from 'vue-router'
import { currentStation, logicalBackTarget, patientTargetFor } from '~/utils/navigation'

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

  it('returns undefined on top-level pages', () => {
    expect(logicalBackTarget(route('/patient'))).toBeUndefined()
    expect(logicalBackTarget(route('/scan-face'))).toBeUndefined()
    expect(logicalBackTarget(route('/medication-stock'))).toBeUndefined()
    expect(logicalBackTarget(route('/orders'))).toBeUndefined()
    expect(logicalBackTarget(route('/settings'))).toBeUndefined()
    expect(logicalBackTarget(route('/account'))).toBeUndefined()
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
