import type { Component } from 'vue'
import type { RouteLocationNormalizedLoaded } from 'vue-router'
import {
  PhEyeglasses,
  PhFlask,
  PhGear,
  PhHeartbeat,
  PhMapPin,
  PhNotebook,
  PhPill,
  PhScan,
  PhStethoscope,
  PhTag,
  PhTooth,
  PhUserCircle,
  PhUserGear,
  PhUsers,
} from '@phosphor-icons/vue'
import type { StationKey } from './journey'
import type { PatientLinkTarget } from './paths'

export interface NavItem {
  name: string
  href: string
  icon: Component
  /** Extra words the universal search matches on. */
  keywords: string
  /** For station entries: the station filter this item opens. */
  station?: StationKey
  /** Only shown to users with the admin role. */
  adminOnly?: boolean
}

/**
 * Sidebar. Stations are not separate pages: each opens the one patient list
 * filtered to who that station is waiting for, so every station works the same
 * way and a patient's whole journey is visible from any of them.
 */
export const NAV_ITEMS: NavItem[] = [
  { name: 'Scan Face', href: paths.scanFace(), icon: PhScan, keywords: 'camera photo face match register new patient registration front desk visit start search find' },
  { name: 'Patients', href: paths.patient(), icon: PhUsers, keywords: 'patients records list demographics queue journey today' },
]

export const STATION_NAV_ITEMS: NavItem[] = [
  { name: 'Vitals', station: 'vitals', href: paths.patient('vitals'), icon: PhHeartbeat, keywords: 'bp blood pressure height weight bmi temperature heart rate pulse glucose hba1c hemocue urine growth chart' },
  { name: 'Vision', station: 'vision', href: paths.patient('vision'), icon: PhEyeglasses, keywords: 'eye eyesight glasses acuity pinhole astigmatism prescription' },
  { name: 'Consults', station: 'consults', href: paths.patient('consults'), icon: PhStethoscope, keywords: 'consult doctor diagnosis plan history' },
  { name: 'Dental', station: 'dental', href: paths.patient('dental'), icon: PhTooth, keywords: 'teeth tooth dentist oral caries' },
  { name: 'Pharmacy', station: 'pharmacy', href: paths.patient('pharmacy'), icon: PhPill, keywords: 'dispense medication prescriptions orders' },
]

export const ADMIN_NAV_ITEMS: NavItem[] = [
  { name: 'Medication Stock', href: paths.medicationStock(), icon: PhPill, keywords: 'medicine drugs pharmacy inventory split expiry' },
  { name: 'Orders', href: paths.orders(), icon: PhNotebook, keywords: 'prescriptions pharmacy' },
  { name: 'Settings', href: paths.settings(), icon: PhGear, keywords: 'configuration admin' },
]

/** Pages reachable from search but not listed in the sidebar. */
export const SUB_PAGES: NavItem[] = [
  { name: 'Medication Brands', href: paths.medicationBrands(), icon: PhTag, keywords: 'brand medicine' },
  { name: 'Medication Active Ingredients', href: paths.medicationActiveIngredients(), icon: PhFlask, keywords: 'ingredient fall below threshold low stock' },
  { name: 'Village Codes', href: paths.settingsVillageCodes(), icon: PhMapPin, keywords: 'village code colour color location' },
  { name: 'Staff Users', href: paths.settingsUsers(), icon: PhUserGear, keywords: 'users staff accounts roles admin password lock two-factor 2fa', adminOnly: true },
  { name: 'My Account', href: paths.account(), icon: PhUserCircle, keywords: 'profile password two-factor 2fa authenticator totp security' },
]

export const ALL_NAV_ITEMS = [...NAV_ITEMS, ...STATION_NAV_ITEMS, ...ADMIN_NAV_ITEMS]

/**
 * The station the user is working in: a station form's `patientLink` meta, or
 * the list's `?station=` filter. Undefined on the unfiltered list and
 * elsewhere.
 */
export function currentStation(route: Pick<RouteLocationNormalizedLoaded, 'path' | 'query' | 'meta'>): StationKey | undefined {
  const meta = route.meta.patientLink as PatientLinkTarget | undefined
  if (meta && meta !== 'patient')
    return meta
  const station = route.query.station
  return route.path === paths.patient() && isStationKey(station) ? station : undefined
}

/**
 * Where opening a patient from search or face match should land: the current
 * station's form, or the patient record (pharmacy has no per-patient form).
 */
export function patientTargetFor(route: Pick<RouteLocationNormalizedLoaded, 'path' | 'query' | 'meta'>): PatientLinkTarget {
  const station = currentStation(route)
  return station && station !== 'pharmacy' ? station : 'patient'
}

export function isNavItemActive(item: NavItem, route: Pick<RouteLocationNormalizedLoaded, 'path' | 'query' | 'meta'>) {
  const station = currentStation(route)
  if (item.station)
    return station === item.station
  if (item.href === paths.patient())
    return !station && (route.path === item.href || route.path.startsWith(`${item.href}/`))
  return route.path === item.href || route.path.startsWith(`${item.href}/`)
}
