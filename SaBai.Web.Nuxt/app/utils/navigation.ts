import type { Component } from 'vue'
import type { RouteLocationNormalizedLoaded } from 'vue-router'
import {
  PhArrowUpRight,
  PhArrowsClockwise,
  PhBell,
  PhClockCounterClockwise,
  PhEyeglasses,
  PhFlask,
  PhGear,
  PhHeartbeat,
  PhHospital,
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
import { isStationKey } from './journey'
import type { PatientLinkTarget } from './paths'
import { paths } from './paths'

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
  /** Shorter name used where the parent already gives the context (nested in the sidebar). */
  navLabel?: string
  /** One line on what the page is for, shown on the Settings page. */
  description?: string
  /** Pages below this one, nested under it in the sidebar. */
  children?: NavItem[]
}

/**
 * Sidebar. Stations are not separate pages: each opens the one patient list
 * filtered to who that station is waiting for, so every station works the same
 * way and a patient's whole journey is visible from any of them.
 */
export const NAV_ITEMS: NavItem[] = [
  { name: 'Scan Face', href: paths.scanFace(), icon: PhScan, keywords: 'camera photo face match register new patient registration front desk visit start search find' },
  { name: 'Patients', href: paths.patient(), icon: PhUsers, keywords: 'patients records list demographics queue journey today' },
  { name: 'Referrals', href: paths.referrals(), icon: PhArrowUpRight, keywords: 'referrals hospital specialist outcome status follow-up tracking' },
]

export const STATION_NAV_ITEMS: NavItem[] = [
  { name: 'Vitals', station: 'vitals', href: paths.patient('vitals'), icon: PhHeartbeat, keywords: 'bp blood pressure height weight bmi temperature heart rate pulse glucose hba1c hemocue urine growth chart' },
  { name: 'Vision', station: 'vision', href: paths.patient('vision'), icon: PhEyeglasses, keywords: 'eye eyesight glasses acuity pinhole astigmatism prescription' },
  { name: 'Consults', station: 'consults', href: paths.patient('consults'), icon: PhStethoscope, keywords: 'consult doctor diagnosis plan history' },
  { name: 'Dental', station: 'dental', href: paths.patient('dental'), icon: PhTooth, keywords: 'teeth tooth dentist oral caries' },
  { name: 'Pharmacy', station: 'pharmacy', href: paths.patient('pharmacy'), icon: PhPill, keywords: 'dispense medication prescriptions orders' },
]

/** Pages under Medication Stock. */
export const MEDICATION_PAGES: NavItem[] = [
  { name: 'Medication Brands', navLabel: 'Brands', href: paths.medicationBrands(), icon: PhTag, keywords: 'brand medicine' },
  { name: 'Medication Active Ingredients', navLabel: 'Active Ingredients', href: paths.medicationActiveIngredients(), icon: PhFlask, keywords: 'ingredient fall below threshold low stock' },
]

/** Pages under Settings, in the order the sidebar and the Settings page list them. */
export const SETTINGS_PAGES: NavItem[] = [
  { name: 'Village Codes', href: paths.settingsVillageCodes(), icon: PhMapPin, keywords: 'village code colour color location', description: 'Manage village codes.' },
  { name: 'Referral Partners', href: paths.settingsReferralPartners(), icon: PhHospital, keywords: 'referral partner clinic hospital specialist', description: 'Manage partner clinics and hospitals for patient referrals.' },
  { name: 'Notifications', href: paths.settingsNotifications(), icon: PhBell, keywords: 'notifications broadcast push alerts announcements message send publish', description: 'Send clinic-wide broadcasts and push notifications to staff devices.' },
  { name: 'Staff Users', href: paths.settingsUsers(), icon: PhUserGear, keywords: 'users staff accounts roles admin password lock two-factor 2fa', description: 'Add staff, assign roles, reset passwords and two-factor, lock accounts.', adminOnly: true },
  { name: 'Audit Log', href: paths.settingsAuditLog(), icon: PhClockCounterClockwise, keywords: 'audit log history changes activity access viewed sign-in login trail', description: 'Who viewed or changed which record, and when staff signed in.', adminOnly: true },
  { name: 'Sync', href: paths.settingsSync(), icon: PhArrowsClockwise, keywords: 'sync offline outbox conflicts download pending changes', description: 'Changes saved on this device, conflicts, and downloading data for offline use.' },
]

export const MY_ACCOUNT_PAGE: NavItem = { name: 'My Account', href: paths.account(), icon: PhUserCircle, keywords: 'profile password two-factor 2fa authenticator totp security', description: 'Change your password and set up two-factor authentication.' }

export const ADMIN_NAV_ITEMS: NavItem[] = [
  { name: 'Medication Stock', href: paths.medicationStock(), icon: PhPill, keywords: 'medicine drugs pharmacy inventory split expiry', children: MEDICATION_PAGES },
  { name: 'Orders', href: paths.orders(), icon: PhNotebook, keywords: 'prescriptions pharmacy' },
  { name: 'Settings', href: paths.settings(), icon: PhGear, keywords: 'configuration admin', children: SETTINGS_PAGES },
]

/** Pages below a sidebar entry (nested under it there) and the account page: all reachable from search. */
export const SUB_PAGES: NavItem[] = [
  ...MEDICATION_PAGES,
  ...SETTINGS_PAGES,
  MY_ACCOUNT_PAGE,
]

export const ALL_NAV_ITEMS = [...NAV_ITEMS, ...STATION_NAV_ITEMS, ...ADMIN_NAV_ITEMS]

/**
 * The station the user is working in: a station form's `patientLink` meta, or
 * the list's `?station=` filter. Undefined on the unfiltered list and
 * elsewhere.
 */
export function currentStation(route: Pick<RouteLocationNormalizedLoaded, 'path' | 'query' | 'meta'>): StationKey | undefined {
  const meta = route.meta?.patientLink as PatientLinkTarget | undefined
  if (meta && meta !== 'patient')
    return meta
  const station = route.query?.station
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

/**
 * Where pressing Escape navigates back to: the parent page in the breadcrumb
 * hierarchy (e.g. a patient record → the patient list, a station form → that
 * station's queue, a settings sub-page → Settings). Undefined on top-level
 * pages.
 */
export function logicalBackTarget(route: Pick<RouteLocationNormalizedLoaded, 'path' | 'query' | 'meta'>): string | undefined {
  const meta = route.meta?.patientLink as PatientLinkTarget | undefined
  if (meta && meta !== 'patient')
    return paths.patient(meta)
  if (route.path.startsWith(`${paths.vitals()}/`))
    return paths.patient('vitals')
  if (route.path.startsWith(`${paths.vision()}/`))
    return paths.patient('vision')
  if (route.path.startsWith(`${paths.consults()}/`))
    return paths.patient('consults')
  if (route.path.startsWith(`${paths.dental()}/`))
    return paths.patient('dental')
  if (route.path.startsWith(`${paths.patient()}/`))
    return paths.patient()
  if (route.path.startsWith(`${paths.referrals()}/`))
    return paths.referrals()
  if (route.path === paths.patient() && isStationKey(route.query?.station))
    return paths.patient()
  if (route.path === paths.medicationBrands() || route.path === paths.medicationActiveIngredients())
    return paths.medicationStock()
  if (route.path.startsWith(`${paths.settings()}/`))
    return paths.settings()
  return undefined
}

/** Whether the route is the item's page or any page nested under it. */
export function isInNavSection(item: NavItem, route: Pick<RouteLocationNormalizedLoaded, 'path' | 'query' | 'meta'>) {
  return isNavItemActive(item, route) || !!item.children?.some(child => isNavItemActive(child, route))
}

export function isNavItemActive(item: NavItem, route: Pick<RouteLocationNormalizedLoaded, 'path' | 'query' | 'meta'>) {
  const station = currentStation(route)
  if (item.station)
    return station === item.station
  if (item.href === paths.patient())
    return !station && (route.path === item.href || route.path.startsWith(`${item.href}/`))
  return route.path === item.href || route.path.startsWith(`${item.href}/`)
}
