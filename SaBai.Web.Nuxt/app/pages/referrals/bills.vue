<script setup lang="ts">
import {
  PhArrowCounterClockwise,
  PhArrowLeft,
  PhDownloadSimple,
  PhMagnifyingGlass,
  PhPencilSimple,
  PhPlus,
  PhWarning,
  PhX,
} from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import type { PnLedgerEntryResponse, PnMemberBalanceResponse } from '~/offline/mutations/referrals'
import type { ReferralListItem } from '@/utils/referral'
import {
  buildPnLedgerCsv,
  buildReferralBillsCsv,
  formatBillAmount,
  formatRiels,
  formatSignedBillAmount,
  formatUsd,
  hasReferralBill,
  isUnattributedBill,
  matchesReferralBill,
  summarizeReferralBills,
  UNASSIGNED_PAYER_KEY,
  type ReferralBillLookup,
} from '@/utils/referralBills'

useHead({ title: "PN Bills & Ledger · Sa'Bai" })

const { data: referrals, isLoading: referralsLoading, isError: referralsError, error: referralsErr } = useReferralsList()
const { data: ledger, isLoading: ledgerLoading, isError: ledgerError, error: ledgerErr } = usePnLedger()
const { data: patientsData } = usePatientsList()
const { data: partners } = useReferralPartnersList()
const { data: staff } = useStaffUsersList()
const { villageCodes } = useVillageCode()

const updateMoney = useUpdateReferralMoney()
const recordRepayment = useRecordPnRepayment()
const reverseEntry = useReversePnLedgerEntry()

const activeTab = useState<'ledger' | 'bills'>('referrals:bills-tab', () => 'ledger')

const filters = useState('referrals:bills-filters', () => ({
  search: '',
  memberWhoPaidId: '',
  unattributedOnly: false,
  referredToId: '',
  villageId: '',
  dateFrom: '',
  dateTo: '',
}))

const patientsById = computed(() => new Map((patientsData.value?.patients ?? []).map(p => [p.id, p])))
const partnersById = computed(() => new Map((partners.value ?? []).map(p => [p.id, p])))
const staffById = computed(() => new Map((staff.value ?? []).map(u => [u.id, u])))

const lookup: ReferralBillLookup = {
  memberName: (id) => {
    if (!id) return undefined
    const u = staffById.value.get(id)
    return u ? u.nickname || u.userName : undefined
  },
  partnerName: id => (id ? partnersById.value.get(id)?.name : undefined),
}

const staffOptions = computed(() =>
  [...(staff.value ?? [])]
    .filter(u => !u.isLocked)
    .sort((a, b) => (a.nickname || a.userName).localeCompare(b.nickname || b.userName)),
)

function shortDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso.slice(0, 10)
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

const balances = computed<PnMemberBalanceResponse[]>(() => ledger.value?.balances ?? [])
const ledgerEntries = computed<PnLedgerEntryResponse[]>(() => {
  const all = ledger.value?.entries ?? []
  if (!filters.value.memberWhoPaidId || filters.value.memberWhoPaidId === UNASSIGNED_PAYER_KEY)
    return all
  return all.filter(e => e.memberId === filters.value.memberWhoPaidId)
})

const ledgerTotals = computed(() => {
  let owedUsdCents = 0
  let owedRiels = 0
  let overpaidUsdCents = 0
  let overpaidRiels = 0
  let owedPeople = 0
  let overpaidPeople = 0

  for (const b of balances.value) {
    if (b.balanceUsd > 0 || b.balanceRiels > 0) owedPeople++
    if (b.balanceUsd < 0 || b.balanceRiels < 0) overpaidPeople++
    if (b.balanceUsd > 0) owedUsdCents += Math.round(b.balanceUsd * 100)
    else if (b.balanceUsd < 0) overpaidUsdCents += Math.round(Math.abs(b.balanceUsd) * 100)
    if (b.balanceRiels > 0) owedRiels += b.balanceRiels
    else if (b.balanceRiels < 0) overpaidRiels += Math.abs(b.balanceRiels)
  }

  return {
    owedPeople,
    owedUsd: owedUsdCents / 100,
    owedRiels,
    overpaidPeople,
    overpaidUsd: overpaidUsdCents / 100,
    overpaidRiels,
  }
})

const allBills = computed<ReferralListItem[]>(() =>
  (referrals.value ?? [])
    .filter(hasReferralBill)
    .map((item) => {
      const patient = patientsById.value.get(item.patientId)
      const tags = item.tags?.length ? item.tags : (patient?.tags ?? [])
      return { ...item, tags, patientDisplayId: patient?.displayId }
    }),
)

const billsSummary = computed(() => summarizeReferralBills(allBills.value))

const filteredBills = computed(() =>
  allBills.value.filter(item =>
    matchesReferralBill(
      item,
      {
        search: filters.value.search,
        memberWhoPaidId: filters.value.memberWhoPaidId || null,
        unattributedOnly: filters.value.unattributedOnly,
        referredToId: filters.value.referredToId || null,
        villageCodeId: filters.value.villageId || null,
        dateFrom: filters.value.dateFrom || null,
        dateTo: filters.value.dateTo || null,
      },
      lookup,
    ),
  ),
)

const hasFilters = computed(
  () =>
    !!filters.value.search
    || !!filters.value.memberWhoPaidId
    || filters.value.unattributedOnly
    || !!filters.value.referredToId
    || !!filters.value.villageId
    || !!filters.value.dateFrom
    || !!filters.value.dateTo,
)

function clearFilters() {
  filters.value = {
    search: '',
    memberWhoPaidId: '',
    unattributedOnly: false,
    referredToId: '',
    villageId: '',
    dateFrom: '',
    dateTo: '',
  }
}

function togglePersonFilter(memberId: string) {
  filters.value.memberWhoPaidId = filters.value.memberWhoPaidId === memberId ? '' : memberId
  filters.value.unattributedOnly = false
}

function formatBalanceBadge(usd: number, riels: number): { label: string, tone: string } {
  const hasPos = usd > 0 || riels > 0
  const hasNeg = usd < 0 || riels < 0
  if (!hasPos && !hasNeg) {
    return { label: 'Settled', tone: 'border-success/40 bg-success-subtle text-success' }
  }
  const parts: string[] = []
  if (usd > 0) parts.push(`Owed ${formatUsd(usd)}`)
  else if (usd < 0) parts.push(`Overpaid ${formatUsd(Math.abs(usd))}`)
  if (riels > 0) parts.push(`Owed ${formatRiels(riels)}`)
  else if (riels < 0) parts.push(`Overpaid ${formatRiels(Math.abs(riels))}`)

  return {
    label: parts.join(' · '),
    tone: hasPos
      ? 'border-warning-border bg-warning-subtle text-warning'
      : 'border-band-border bg-accent text-accent-foreground',
  }
}

// ── Repay by person dialog ───────────────────────────────────────────────

const repayDialog = ref<{
  open: boolean
  memberId: string
  amountUsd: string
  amountRiels: string
  notes: string
}>({
  open: false,
  memberId: '',
  amountUsd: '',
  amountRiels: '',
  notes: '',
})

const selectedRepayBalance = computed(() =>
  balances.value.find(b => b.memberId === repayDialog.value.memberId),
)

const projectedBalance = computed(() => {
  const curUsd = selectedRepayBalance.value?.balanceUsd ?? 0
  const curRiels = selectedRepayBalance.value?.balanceRiels ?? 0
  const payUsd = repayDialog.value.amountUsd.trim() ? Number(repayDialog.value.amountUsd) : 0
  const payRiels = repayDialog.value.amountRiels.trim() ? Number(repayDialog.value.amountRiels) : 0
  return {
    usd: Math.round((curUsd - (Number.isFinite(payUsd) ? payUsd : 0)) * 100) / 100,
    riels: curRiels - (Number.isFinite(payRiels) ? Math.round(payRiels) : 0),
  }
})

function openRepayDialog(memberId = '') {
  const bal = balances.value.find(b => b.memberId === memberId)
  repayDialog.value = {
    open: true,
    memberId,
    amountUsd: bal && bal.balanceUsd > 0 ? bal.balanceUsd.toFixed(2) : '',
    amountRiels: bal && bal.balanceRiels > 0 ? String(bal.balanceRiels) : '',
    notes: '',
  }
}

async function submitRepay() {
  const memberId = repayDialog.value.memberId
  if (!memberId) {
    toast.error('Select a PN member to repay.')
    return
  }
  const usd = repayDialog.value.amountUsd.trim() ? Number(repayDialog.value.amountUsd) : 0
  const riels = repayDialog.value.amountRiels.trim() ? Number(repayDialog.value.amountRiels) : 0
  if (!Number.isFinite(usd) || usd < 0 || !Number.isFinite(riels) || riels < 0 || !Number.isInteger(riels)) {
    toast.error('Enter valid non-negative USD and integer KHR amounts.')
    return
  }
  if (usd <= 0 && riels <= 0) {
    toast.error('Enter a positive USD or KHR repayment amount.')
    return
  }

  const memberName = lookup.memberName(memberId) ?? selectedRepayBalance.value?.memberName ?? 'PN member'
  try {
    const outcome = await recordRepayment.mutateAsync({
      memberId,
      memberName,
      amountUsd: usd > 0 ? usd : null,
      amountRiels: riels > 0 ? riels : null,
      notes: repayDialog.value.notes.trim() || null,
    })
    repayDialog.value.open = false
    notifySaved(outcome, `Recorded ${formatBillAmount(usd, riels)} repayment to ${memberName}`)
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Could not record repayment.'))
  }
}

// ── Reverse ledger entry dialog ──────────────────────────────────────────

const reverseDialog = ref<{
  open: boolean
  entry: PnLedgerEntryResponse | null
  notes: string
}>({
  open: false,
  entry: null,
  notes: '',
})

function openReverseDialog(entry: PnLedgerEntryResponse) {
  reverseDialog.value = { open: true, entry, notes: '' }
}

async function submitReverse() {
  const entry = reverseDialog.value.entry
  if (!entry) return
  try {
    const outcome = await reverseEntry.mutateAsync({
      id: entry.id,
      notes: reverseDialog.value.notes.trim() || null,
    })
    reverseDialog.value.open = false
    notifySaved(outcome, 'Ledger entry reversed')
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Could not reverse ledger entry.'))
  }
}

// ── Edit referral bill dialog ────────────────────────────────────────────

const editDialog = ref<{
  open: boolean
  item: ReferralListItem | null
  memberWhoPaidId: string
  amountPaidUsd: string
  amountPaidRiels: string
}>({
  open: false,
  item: null,
  memberWhoPaidId: '',
  amountPaidUsd: '',
  amountPaidRiels: '',
})

function openEditBill(item: ReferralListItem) {
  editDialog.value = {
    open: true,
    item,
    memberWhoPaidId: item.memberWhoPaidId ?? '',
    amountPaidUsd: item.amountPaidUsd != null ? String(item.amountPaidUsd) : '',
    amountPaidRiels: item.amountPaidRiels != null ? String(item.amountPaidRiels) : '',
  }
}

async function saveEditBill() {
  const item = editDialog.value.item
  if (!item) return

  const parsedUsd = editDialog.value.amountPaidUsd.trim() ? Number(editDialog.value.amountPaidUsd) : null
  const parsedRiels = editDialog.value.amountPaidRiels.trim() ? Number(editDialog.value.amountPaidRiels) : null
  if (parsedUsd !== null && (!Number.isFinite(parsedUsd) || parsedUsd < 0)) {
    toast.error('USD amount must be a non-negative number.')
    return
  }
  if (parsedRiels !== null && (!Number.isFinite(parsedRiels) || parsedRiels < 0 || !Number.isInteger(parsedRiels))) {
    toast.error('KHR amount must be a non-negative whole number.')
    return
  }

  try {
    const outcome = await updateMoney.mutateAsync({
      id: item.id,
      patientName: item.patientName,
      amountPaidUsd: parsedUsd,
      amountPaidRiels: parsedRiels,
      memberWhoPaidId: editDialog.value.memberWhoPaidId || null,
    })
    editDialog.value.open = false
    notifySaved(outcome, 'Bill payment updated and ledger adjusted')
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Could not update bill payment.'))
  }
}

function downloadCsv(filename: string, csv: string) {
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  URL.revokeObjectURL(url)
}

function exportCsv() {
  const today = new Date().toISOString().slice(0, 10)
  if (activeTab.value === 'ledger') {
    downloadCsv(`pn-ledger-${today}.csv`, buildPnLedgerCsv(ledgerEntries.value))
  }
  else {
    downloadCsv(`pn-referral-bills-${today}.csv`, buildReferralBillsCsv(filteredBills.value, lookup))
  }
}
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      title="PN Bills & Ledger"
      description="Double-entry ledger of referral bills paid by PNs and repayments made by person."
      :breadcrumbs="[
        { label: 'Home', to: '/' },
        { label: 'Referrals', to: paths.referrals() },
        { label: 'PN Bills & Ledger' },
      ]"
    >
      <template #actions>
        <Button as-child variant="outline" size="sm">
          <NuxtLink :to="paths.referrals()">
            <PhArrowLeft aria-hidden="true" /> Referrals
          </NuxtLink>
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          :disabled="activeTab === 'ledger' ? !ledgerEntries.length : !filteredBills.length"
          @click="exportCsv"
        >
          <PhDownloadSimple aria-hidden="true" /> Export CSV
        </Button>
        <Button type="button" variant="success" size="sm" @click="openRepayDialog(filters.memberWhoPaidId || '')">
          <PhPlus aria-hidden="true" /> Record repayment
        </Button>
      </template>
    </PageHeader>

    <StateMessage v-if="referralsLoading || ledgerLoading" kind="loading" message="Loading PN ledger…" />
    <StateMessage
      v-else-if="referralsError || ledgerError"
      kind="error"
      :message="describeLoadError(ledgerErr ?? referralsErr, 'Could not load PN bills and ledger.')"
    />

    <template v-else>
      <!-- Summary strip -->
      <section aria-label="Ledger totals" class="grid grid-cols-2 gap-px border border-border bg-border md:grid-cols-4">
        <div class="bg-card px-3 py-2.5">
          <p class="text-[0.6875rem] font-bold uppercase tracking-[0.06em] text-muted-foreground">
            Owed to PNs
          </p>
          <p class="mt-0.5 text-base font-bold tabular-nums text-warning">
            {{ formatBillAmount(ledgerTotals.owedUsd, ledgerTotals.owedRiels) }}
          </p>
          <p class="text-xs text-muted-foreground tabular-nums">
            {{ ledgerTotals.owedPeople }} {{ ledgerTotals.owedPeople === 1 ? 'person' : 'people' }}
          </p>
        </div>

        <div class="bg-card px-3 py-2.5">
          <p class="text-[0.6875rem] font-bold uppercase tracking-[0.06em] text-muted-foreground">
            Overpaid credits
          </p>
          <p class="mt-0.5 text-base font-bold tabular-nums text-chrome">
            {{ formatBillAmount(ledgerTotals.overpaidUsd, ledgerTotals.overpaidRiels) }}
          </p>
          <p class="text-xs text-muted-foreground tabular-nums">
            {{ ledgerTotals.overpaidPeople }} {{ ledgerTotals.overpaidPeople === 1 ? 'person' : 'people' }}
          </p>
        </div>

        <div class="bg-card px-3 py-2.5">
          <p class="text-[0.6875rem] font-bold uppercase tracking-[0.06em] text-muted-foreground">
            Total referral bills
          </p>
          <p class="mt-0.5 text-base font-bold tabular-nums text-foreground">
            {{ formatBillAmount(billsSummary.totalUsd, billsSummary.totalRiels) }}
          </p>
          <p class="text-xs text-muted-foreground tabular-nums">
            {{ billsSummary.totalCount }} {{ billsSummary.totalCount === 1 ? 'bill' : 'bills' }}
          </p>
        </div>

        <button
          type="button"
          class="bg-card px-3 py-2.5 text-left transition-colors hover:bg-accent/50"
          @click="() => { activeTab = 'bills'; filters.unattributedOnly = !filters.unattributedOnly; filters.memberWhoPaidId = '' }"
        >
          <p class="flex items-center gap-1 text-[0.6875rem] font-bold uppercase tracking-[0.06em] text-muted-foreground">
            <PhWarning v-if="billsSummary.unattributedCount" class="size-3.5 text-destructive" aria-hidden="true" />
            Unattributed bills
          </p>
          <p class="mt-0.5 text-base font-bold tabular-nums" :class="billsSummary.unattributedCount ? 'text-destructive' : 'text-muted-foreground'">
            {{ formatBillAmount(billsSummary.unattributedUsd, billsSummary.unattributedRiels) }}
          </p>
          <p class="text-xs text-muted-foreground tabular-nums">
            {{ billsSummary.unattributedCount }} without PN assigned
          </p>
        </button>
      </section>

      <!-- Per-person balances -->
      <AppPanel title="Balances by person" description="Repay by person. Underpaying leaves an owed balance; overpaying records a credit against future bills.">
        <div v-if="!balances.length" class="py-4 text-center text-xs text-muted-foreground">
          No PN ledger entries yet. Record a referral bill payment or click "Record repayment".
        </div>
        <div v-else class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          <div
            v-for="b in balances"
            :key="b.memberId"
            class="flex flex-col justify-between gap-2 border p-3 transition-colors"
            :class="filters.memberWhoPaidId === b.memberId ? 'border-primary bg-accent/40' : 'border-border bg-card'"
          >
            <div class="flex items-start justify-between gap-2">
              <button type="button" class="min-w-0 text-left" @click="togglePersonFilter(b.memberId)">
                <p class="truncate text-sm font-bold text-chrome hover:underline">
                  {{ b.memberName }}
                </p>
                <p class="text-xs text-muted-foreground tabular-nums">
                  {{ b.entryCount }} ledger {{ b.entryCount === 1 ? 'entry' : 'entries' }}
                </p>
              </button>
              <Badge variant="outline" :class="formatBalanceBadge(b.balanceUsd, b.balanceRiels).tone">
                {{ formatBalanceBadge(b.balanceUsd, b.balanceRiels).label }}
              </Badge>
            </div>

            <dl class="grid grid-cols-2 gap-1 text-xs tabular-nums">
              <div>
                <dt class="text-muted-foreground">Bills paid</dt>
                <dd class="font-medium text-foreground">{{ formatBillAmount(b.totalBillsUsd, b.totalBillsRiels) }}</dd>
              </div>
              <div>
                <dt class="text-muted-foreground">Repaid</dt>
                <dd class="font-medium text-foreground">{{ formatBillAmount(b.totalRepaidUsd, b.totalRepaidRiels) }}</dd>
              </div>
            </dl>

            <div class="flex items-center justify-between gap-2 border-t border-border pt-2">
              <Button type="button" size="sm" variant="ghost" @click="togglePersonFilter(b.memberId)">
                {{ filters.memberWhoPaidId === b.memberId ? 'Show all' : 'Filter' }}
              </Button>
              <Button type="button" size="sm" variant="outline" @click="openRepayDialog(b.memberId)">
                Repay
              </Button>
            </div>
          </div>
        </div>
      </AppPanel>

      <!-- Tabs: Ledger vs Referral Bills -->
      <div class="flex flex-wrap items-center justify-between gap-2">
        <div class="inline-flex border border-border bg-card p-0.5">
          <button
            type="button"
            class="px-3 py-1.5 text-xs font-semibold transition-colors"
            :class="activeTab === 'ledger' ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'"
            @click="activeTab = 'ledger'"
          >
            Double-entry ledger ({{ ledgerEntries.length }})
          </button>
          <button
            type="button"
            class="px-3 py-1.5 text-xs font-semibold transition-colors"
            :class="activeTab === 'bills' ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'"
            @click="activeTab = 'bills'"
          >
            Referral bills ({{ filteredBills.length }})
          </button>
        </div>

        <div v-if="filters.memberWhoPaidId || filters.unattributedOnly" class="flex items-center gap-2 text-xs">
          <span class="text-muted-foreground">
            Filtered by:
            <strong class="text-foreground">
              {{ filters.unattributedOnly ? 'Unattributed bills' : lookup.memberName(filters.memberWhoPaidId) ?? 'Selected PN' }}
            </strong>
          </span>
          <Button type="button" size="sm" variant="ghost" @click="clearFilters">
            <PhX aria-hidden="true" /> Clear
          </Button>
        </div>
      </div>

      <!-- Tab 1: Double-entry ledger -->
      <AppPanel v-if="activeTab === 'ledger'" title="Double-entry ledger" flush>
        <div v-if="!ledgerEntries.length" class="px-3 py-10 text-center text-xs text-muted-foreground">
          No ledger entries match the current filter.
        </div>
        <div v-else class="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>PN Member</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Debit → Credit</TableHead>
                <TableHead class="text-right">Amount</TableHead>
                <TableHead>Notes / Referral</TableHead>
                <TableHead class="text-right">Action</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow v-for="e in ledgerEntries" :key="e.id" :class="e.isReversed ? 'opacity-60' : ''">
                <TableCell class="whitespace-nowrap text-xs tabular-nums text-muted-foreground">
                  {{ shortDate(e.createdAt) }}
                </TableCell>
                <TableCell class="text-xs font-medium text-foreground">
                  {{ e.memberName }}
                </TableCell>
                <TableCell>
                  <Badge
                    variant="outline"
                    class="text-[0.6875rem]"
                    :class="e.entryType === 'Repayment' ? 'border-success/40 bg-success-subtle text-success' : e.entryType === 'Adjustment' ? 'border-band-border bg-accent text-accent-foreground' : 'border-border text-foreground'"
                  >
                    {{ e.entryType }}
                  </Badge>
                </TableCell>
                <TableCell class="whitespace-nowrap font-mono text-[0.6875rem] text-muted-foreground">
                  {{ e.debitAccount }} → {{ e.creditAccount }}
                </TableCell>
                <TableCell class="whitespace-nowrap text-right text-xs font-semibold tabular-nums" :class="e.amountUsd < 0 || e.amountRiels < 0 ? 'text-success' : 'text-foreground'">
                  {{ formatSignedBillAmount(e.amountUsd, e.amountRiels) }}
                </TableCell>
                <TableCell class="max-w-xs text-xs text-muted-foreground">
                  <div class="flex flex-wrap items-center gap-1.5">
                    <NuxtLink
                      v-if="e.referralId"
                      :to="paths.referralsDetail(e.referralId)"
                      class="font-medium text-primary hover:underline"
                    >
                      {{ e.patientName || 'View referral' }}
                    </NuxtLink>
                    <span v-if="e.notes">{{ e.referralId ? '—' : '' }} {{ e.notes }}</span>
                    <Badge v-if="e.isReversed" variant="outline" class="text-[0.625rem]">Reversed</Badge>
                  </div>
                </TableCell>
                <TableCell class="text-right">
                  <Button
                    v-if="e.entryType !== 'Bill' && !e.isReversed && !e.reversedEntryId"
                    type="button"
                    size="sm"
                    variant="ghost"
                    @click="openReverseDialog(e)"
                  >
                    <PhArrowCounterClockwise aria-hidden="true" /> Reverse
                  </Button>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
      </AppPanel>

      <!-- Tab 2: Referral bills -->
      <AppPanel v-else title="Referral bills" flush>
        <div class="flex flex-wrap items-end gap-2 border-b border-border p-3">
          <div class="relative min-w-48 flex-1">
            <PhMagnifyingGlass class="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              v-model="filters.search"
              type="search"
              placeholder="Search patient, ID, clinic, PN…"
              class="pl-8"
              aria-label="Search referral bills"
            />
          </div>

          <NativeSelect v-model="filters.memberWhoPaidId" aria-label="Filter by PN member" class="w-40">
            <NativeSelectOption value="">All PNs</NativeSelectOption>
            <NativeSelectOption :value="UNASSIGNED_PAYER_KEY">Unassigned</NativeSelectOption>
            <NativeSelectOption v-for="u in staffOptions" :key="u.id" :value="u.id">
              {{ u.nickname || u.userName }}
            </NativeSelectOption>
          </NativeSelect>

          <NativeSelect v-model="filters.referredToId" aria-label="Filter by clinic" class="w-40">
            <NativeSelectOption value="">All clinics</NativeSelectOption>
            <NativeSelectOption v-for="p in partners ?? []" :key="p.id" :value="p.id">
              {{ p.name }}
            </NativeSelectOption>
          </NativeSelect>

          <NativeSelect v-model="filters.villageId" aria-label="Filter by village" class="w-32">
            <NativeSelectOption value="">All villages</NativeSelectOption>
            <NativeSelectOption v-for="v in villageCodes" :key="v.id" :value="v.id">
              {{ v.code }}
            </NativeSelectOption>
          </NativeSelect>

          <Input v-model="filters.dateFrom" type="date" aria-label="From date" class="w-36" />
          <Input v-model="filters.dateTo" type="date" aria-label="To date" class="w-36" />

          <Button v-if="hasFilters" type="button" size="sm" variant="ghost" @click="clearFilters">
            Clear
          </Button>
        </div>

        <div v-if="!filteredBills.length" class="px-3 py-10 text-center text-xs text-muted-foreground">
          No referral bills match these filters.
        </div>
        <div v-else class="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Patient</TableHead>
                <TableHead>Clinic / Referred for</TableHead>
                <TableHead>PN Who Paid</TableHead>
                <TableHead class="text-right">Amount</TableHead>
                <TableHead class="text-right">Edit</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow v-for="item in filteredBills" :key="item.id">
                <TableCell class="whitespace-nowrap text-xs tabular-nums text-muted-foreground">
                  {{ shortDate(item.createdAt) }}
                </TableCell>
                <TableCell class="text-xs">
                  <NuxtLink :to="paths.referralsDetail(item.id)" class="font-semibold text-primary hover:underline">
                    {{ item.patientName }}
                  </NuxtLink>
                  <div class="text-muted-foreground">
                    <PatientCode
                      :village-code="item.villageCode"
                      :village-color-hex="item.villageColorHex"
                      :id="item.patientId"
                      :display-id="item.patientDisplayId ?? item.villageCode"
                    />
                  </div>
                </TableCell>
                <TableCell class="text-xs">
                  <p class="font-medium text-foreground">{{ lookup.partnerName(item.referredToId) ?? '—' }}</p>
                  <p class="text-muted-foreground">{{ item.referredFor }}</p>
                </TableCell>
                <TableCell class="text-xs">
                  <span v-if="item.memberWhoPaidId" class="font-medium text-foreground">
                    {{ lookup.memberName(item.memberWhoPaidId) ?? 'Staff' }}
                  </span>
                  <Badge v-else variant="outline" class="border-destructive/40 text-destructive">
                    Unassigned
                  </Badge>
                </TableCell>
                <TableCell class="whitespace-nowrap text-right text-xs font-semibold tabular-nums text-foreground">
                  {{ formatBillAmount(item.amountPaidUsd, item.amountPaidRiels) }}
                </TableCell>
                <TableCell class="text-right">
                  <Button type="button" size="sm" variant="ghost" @click="openEditBill(item)">
                    <PhPencilSimple aria-hidden="true" /> Edit
                  </Button>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
      </AppPanel>
    </template>

    <!-- Record repayment dialog -->
    <Dialog v-model:open="repayDialog.open">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Record repayment by person</DialogTitle>
          <DialogDescription>
            Record cash repaid to a PN member. You can underpay (partial) or overpay (credit); the ledger tracks the net balance.
          </DialogDescription>
        </DialogHeader>

        <div class="flex flex-col gap-3">
          <FormField v-slot="{ id }" label="PN Member">
            <NativeSelect :id="id" v-model="repayDialog.memberId">
              <NativeSelectOption value="">Select PN member…</NativeSelectOption>
              <NativeSelectOption v-for="u in staffOptions" :key="u.id" :value="u.id">
                {{ u.nickname || u.userName }}
              </NativeSelectOption>
            </NativeSelect>
          </FormField>

          <div v-if="repayDialog.memberId" class="border border-border bg-muted/40 px-3 py-2 text-xs">
            <p class="flex items-center justify-between">
              <span class="text-muted-foreground">Current balance:</span>
              <span class="font-semibold tabular-nums">
                {{ formatBalanceBadge(selectedRepayBalance?.balanceUsd ?? 0, selectedRepayBalance?.balanceRiels ?? 0).label }}
              </span>
            </p>
            <p class="mt-1 flex items-center justify-between">
              <span class="text-muted-foreground">Balance after repayment:</span>
              <span class="font-semibold tabular-nums">
                {{ formatBalanceBadge(projectedBalance.usd, projectedBalance.riels).label }}
              </span>
            </p>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <FormField v-slot="{ id }" label="Amount USD">
              <Input :id="id" v-model="repayDialog.amountUsd" type="number" step="0.01" min="0" placeholder="0.00" />
            </FormField>
            <FormField v-slot="{ id }" label="Amount KHR">
              <Input :id="id" v-model="repayDialog.amountRiels" type="number" step="100" min="0" placeholder="0" />
            </FormField>
          </div>

          <FormField v-slot="{ id }" label="Notes">
            <Textarea :id="id" v-model="repayDialog.notes" :rows="2" placeholder="e.g. End-of-day cash settlement" />
          </FormField>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" @click="repayDialog.open = false">Cancel</Button>
          <Button type="button" variant="success" :disabled="recordRepayment.isPending.value" @click="submitRepay">
            {{ recordRepayment.isPending.value ? 'Saving…' : 'Record repayment' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Reverse ledger entry dialog -->
    <Dialog v-model:open="reverseDialog.open">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Reverse ledger entry</DialogTitle>
          <DialogDescription>
            Creates an equal-and-opposite double-entry adjustment for {{ reverseDialog.entry?.memberName }}.
          </DialogDescription>
        </DialogHeader>

        <div v-if="reverseDialog.entry" class="flex flex-col gap-3">
          <div class="border border-border bg-muted/40 px-3 py-2 text-xs tabular-nums">
            Original: <strong>{{ reverseDialog.entry.entryType }}</strong>
            ({{ formatSignedBillAmount(reverseDialog.entry.amountUsd, reverseDialog.entry.amountRiels) }})
          </div>
          <FormField v-slot="{ id }" label="Reason / notes">
            <Textarea :id="id" v-model="reverseDialog.notes" :rows="2" placeholder="e.g. Entered wrong amount" />
          </FormField>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" @click="reverseDialog.open = false">Cancel</Button>
          <Button type="button" variant="destructive" :disabled="reverseEntry.isPending.value" @click="submitReverse">
            {{ reverseEntry.isPending.value ? 'Reversing…' : 'Confirm reversal' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Edit referral bill dialog -->
    <Dialog v-model:open="editDialog.open">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit referral payment</DialogTitle>
          <DialogDescription>
            Updating the amount or PN automatically posts a double-entry adjustment to the ledger.
          </DialogDescription>
        </DialogHeader>

        <div v-if="editDialog.item" class="flex flex-col gap-3">
          <FormField v-slot="{ id }" label="PN Member Who Paid">
            <NativeSelect :id="id" v-model="editDialog.memberWhoPaidId">
              <NativeSelectOption value="">Unassigned</NativeSelectOption>
              <NativeSelectOption v-for="u in staffOptions" :key="u.id" :value="u.id">
                {{ u.nickname || u.userName }}
              </NativeSelectOption>
            </NativeSelect>
          </FormField>

          <div class="grid grid-cols-2 gap-3">
            <FormField v-slot="{ id }" label="Paid USD">
              <Input :id="id" v-model="editDialog.amountPaidUsd" type="number" step="0.01" min="0" placeholder="0.00" />
            </FormField>
            <FormField v-slot="{ id }" label="Paid KHR">
              <Input :id="id" v-model="editDialog.amountPaidRiels" type="number" step="100" min="0" placeholder="0" />
            </FormField>
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" @click="editDialog.open = false">Cancel</Button>
          <Button type="button" variant="success" :disabled="updateMoney.isPending.value" @click="saveEditBill">
            {{ updateMoney.isPending.value ? 'Saving…' : 'Save payment' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
