import type { Component } from 'vue'
import {
  PhEyeglasses,
  PhGear,
  PhHeartbeat,
  PhNotebook,
  PhPill,
  PhScan,
  PhStethoscope,
  PhTooth,
  PhUser,
} from '@phosphor-icons/vue'

export interface NavItem {
  name: string
  href: string
  icon: Component
  /** Alt+<key> jumps to this activity. */
  key: string
}

/** Activities in the sidebar, in the same order as SaBai.Web. */
export const NAV_ITEMS: NavItem[] = [
  { name: 'Scan Face', href: paths.scanFace(), icon: PhScan, key: '1' },
  { name: 'Patient', href: paths.patient(), icon: PhUser, key: '2' },
  { name: 'Vitals', href: paths.vitals(), icon: PhHeartbeat, key: '3' },
  { name: 'Vision', href: paths.vision(), icon: PhEyeglasses, key: '4' },
  { name: 'Consults', href: paths.consults(), icon: PhStethoscope, key: '5' },
  { name: 'Dental', href: paths.dental(), icon: PhTooth, key: '6' },
  { name: 'Medication Stock', href: paths.medicationStock(), icon: PhPill, key: '7' },
  { name: 'Orders', href: paths.orders(), icon: PhNotebook, key: '8' },
  { name: 'Settings', href: paths.settings(), icon: PhGear, key: '9' },
]

export function isNavItemActive(item: NavItem, path: string) {
  return path === item.href || path.startsWith(`${item.href}/`)
}
