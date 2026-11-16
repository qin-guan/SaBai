export type ChangelogCategory = 'feature' | 'improvement' | 'fix' | 'security' | 'platform'

export interface ChangelogEntry {
  title: string
  description: string
  category: ChangelogCategory
  prs?: number[]
}

export interface ChangelogDay {
  date: string
  label: string
  summary: string
  entries: ChangelogEntry[]
}

export const CHANGELOG_CATEGORY_META: Record<ChangelogCategory, {
  label: string
  variant: 'default' | 'secondary' | 'destructive' | 'outline'
  badgeClass?: string
}> = {
  feature: {
    label: 'Feature',
    variant: 'default',
  },
  improvement: {
    label: 'Improvement',
    variant: 'secondary',
  },
  fix: {
    label: 'Fix',
    variant: 'outline',
    badgeClass: 'border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-200',
  },
  security: {
    label: 'Security',
    variant: 'destructive',
  },
  platform: {
    label: 'Platform',
    variant: 'outline',
  },
}

export const CHANGELOG_DAYS: ChangelogDay[] = [
  {
    date: '2026-11-16',
    label: 'November 16, 2026',
    summary: 'PWA update handling on login and Point Nurse referral filtering',
    entries: [
      {
        title: 'Surface and auto-apply PWA updates on the login page',
        description: 'Pending Progressive Web App updates are now surfaced and automatically applied on the login screen so clinic devices start on the latest build.',
        category: 'fix',
        prs: [61],
      },
      {
        title: 'Filter referrals by Point Nurse and keep registration submit pending',
        description: 'Added Point Nurse filtering on referral runs and kept the registration submit button in a pending state until the request completes to prevent duplicate submissions.',
        category: 'feature',
        prs: [59],
      },
    ],
  },
  {
    date: '2026-11-15',
    label: 'November 15, 2026',
    summary: 'PN referral bills consolidation and double-entry ledger',
    entries: [
      {
        title: 'PN referral bills consolidation and reimbursement management',
        description: 'Added the PN Referral Bills page to consolidate referral expenses, review clinic-owed and PN-owed balances, and settle reimbursements in USD and KHR.',
        category: 'feature',
        prs: [55],
      },
      {
        title: 'Per-person double-entry PN ledger',
        description: 'Replaced ad-hoc referral reimbursements with a per-person double-entry Point Nurse ledger for accurate balance tracking and settlements.',
        category: 'improvement',
        prs: [57],
      },
      {
        title: 'NumericInput and safe number coercion in PN ledger modals',
        description: 'Updated PN ledger modals to use NumericInput with safe number coercion for reliable currency entry.',
        category: 'fix',
        prs: [58],
      },
    ],
  },
  {
    date: '2026-11-14',
    label: 'November 14, 2026',
    summary: 'Offline-first PWA, patient tags, HIPAA privacy controls, and .NET 11',
    entries: [
      {
        title: 'Offline-first PWA with cached reads, REST outbox, and UUIDv7 keys',
        description: 'Added offline-first support with local query caching, a REST mutation outbox, conflict handling, and a Sync status page under Settings.',
        category: 'feature',
        prs: [46, 52, 53],
      },
      {
        title: 'Patient tags, inline creation, and multiselect table filtering',
        description: 'Added tag browsing and filtering across patients and staff, inline tag search and creation in TagInput, multiselect tag/status filters, and a compact tags column in the patient table.',
        category: 'feature',
        prs: [45, 48, 50, 56],
      },
      {
        title: 'HL7 FHIR R4 facade and HIPAA privacy & consent controls',
        description: 'Added HL7 FHIR R4 interoperability endpoints and frontend views for HIPAA access logs, patient consents (including during registration), and disclosures.',
        category: 'feature',
        prs: [40, 44, 54],
      },
      {
        title: 'Biometric consent, file upload, and telemetry security fixes',
        description: 'Hardened biometric consent enforcement, file upload validation, and telemetry handling alongside offline outbox fixes.',
        category: 'security',
        prs: [53],
      },
      {
        title: 'Single-form referral save, vitals height fix, and table polish',
        description: 'Unified referrals into a single form with one save action, fixed vitals height entry, removed duplicate patient filters from station tables, and aligned referrals and notifications pages with app conventions.',
        category: 'improvement',
        prs: [42, 43, 47],
      },
      {
        title: 'Upgrade backend to .NET 11 and streamline Compose setup',
        description: 'Upgraded the API and test projects to .NET 11, pinned EF Core packages to 10.0.12 for Npgsql compatibility, and simplified Docker Compose configuration.',
        category: 'platform',
        prs: [41, 49, 51],
      },
    ],
  },
  {
    date: '2026-11-13',
    label: 'November 13, 2026',
    summary: 'Referrals overhaul, consultation PDFs, and PWA push notifications',
    entries: [
      {
        title: 'Referrals overhaul, partner management, and Point Nurse role',
        description: 'Overhauled referral tracking with partner clinic/hospital management, the Point Nurse (PN) role, consultation PDF export, direct links to originating consults, two-tier visit card headers, and mobile layout polish.',
        category: 'feature',
        prs: [33, 35, 36, 37, 38, 39],
      },
      {
        title: 'PWA support with Web Push notifications and clinic broadcasts',
        description: 'Added installable PWA support with Web Push notifications and a Settings → Notifications UI to compose and send clinic-wide broadcasts.',
        category: 'feature',
        prs: [31, 32],
      },
      {
        title: 'Mobile audit log view and stacked table layout',
        description: 'Fixed the audit log layout and stacked table rows on mobile viewports.',
        category: 'fix',
        prs: [34],
      },
    ],
  },
  {
    date: '2026-11-12',
    label: 'November 12, 2026',
    summary: 'Referral outcome API support',
    entries: [
      {
        title: 'PATCH /referrals/{id}/outcome endpoint',
        description: 'Added the backend endpoint to record and update referral outcomes.',
        category: 'feature',
        prs: [29],
      },
    ],
  },
  {
    date: '2026-11-11',
    label: 'November 11, 2026',
    summary: 'Khmer language support, Esc navigation, and shared dental files',
    entries: [
      {
        title: 'App-wide Khmer text, font, and search support',
        description: 'Added Noto Sans Khmer fonts across the web app and consultation PDFs, Khmer numeral/text utilities, and Khmer-aware patient and universal search.',
        category: 'feature',
        prs: [27],
      },
      {
        title: 'Esc keyboard shortcut for logical back navigation',
        description: 'Pressing Esc now navigates up the page hierarchy (for example, from a station form back to its station queue or from a patient record to the patient list).',
        category: 'improvement',
        prs: [26],
      },
      {
        title: 'Upstream sync and dental image file links',
        description: 'Synced upstream updates and generalized stored files so dental images are linked as shared Files records.',
        category: 'improvement',
        prs: [24],
      },
    ],
  },
  {
    date: '2026-11-10',
    label: 'November 10, 2026',
    summary: 'Station queue view simplification',
    entries: [
      {
        title: 'Simplify station queue view and remove calendar-day cutoff',
        description: 'Simplified the station queue view and removed the calendar-day cutoff so active visits remain visible across days.',
        category: 'improvement',
        prs: [25],
      },
    ],
  },
]
