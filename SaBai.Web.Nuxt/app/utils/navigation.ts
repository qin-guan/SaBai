import type { Component } from 'vue'
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
  PhUser,
} from '@phosphor-icons/vue'

export interface NavItem {
  name: string
  href: string
  icon: Component
  /** Extra words the universal search matches on. */
  keywords: string
}

/** Activities in the sidebar, in the same order as SaBai.Web. */
export const NAV_ITEMS: NavItem[] = [
  { name: 'Scan Face', href: paths.scanFace(), icon: PhScan, keywords: 'camera photo face match register new patient registration front desk visit' },
  { name: 'Patient', href: paths.patient(), icon: PhUser, keywords: 'patients records list demographics' },
  { name: 'Vitals', href: paths.vitals(), icon: PhHeartbeat, keywords: 'bp blood pressure height weight bmi temperature heart rate pulse glucose hba1c hemocue urine growth chart' },
  { name: 'Vision', href: paths.vision(), icon: PhEyeglasses, keywords: 'eye eyesight glasses acuity pinhole astigmatism prescription' },
  { name: 'Consults', href: paths.consults(), icon: PhStethoscope, keywords: 'consult doctor diagnosis plan history' },
  { name: 'Dental', href: paths.dental(), icon: PhTooth, keywords: 'teeth tooth dentist oral caries' },
  { name: 'Medication Stock', href: paths.medicationStock(), icon: PhPill, keywords: 'medicine drugs pharmacy inventory split expiry' },
  { name: 'Orders', href: paths.orders(), icon: PhNotebook, keywords: 'prescriptions pharmacy' },
  { name: 'Settings', href: paths.settings(), icon: PhGear, keywords: 'configuration admin' },
]

/** Pages reachable from search but not listed in the sidebar. */
export const SUB_PAGES: NavItem[] = [
  { name: 'Medication Brands', href: paths.medicationBrands(), icon: PhTag, keywords: 'brand medicine' },
  { name: 'Medication Active Ingredients', href: paths.medicationActiveIngredients(), icon: PhFlask, keywords: 'ingredient fall below threshold low stock' },
  { name: 'Village Codes', href: paths.settingsVillageCodes(), icon: PhMapPin, keywords: 'village code colour color location' },
]

export function isNavItemActive(item: NavItem, path: string) {
  return path === item.href || path.startsWith(`${item.href}/`)
}
