<script setup lang="ts">
import {
  PhArrowLeft,
  PhCheck,
  PhCheckCircle,
  PhDownloadSimple,
  PhMagnifyingGlass,
  PhPencilSimple,
  PhWarning,
  PhX,
} from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
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
import type { ReferralListItem } from '@/utils/referral'
import {
  buildReferralBillsCsv,
  consolidateBillsByPn,
  formatBillAmount,
  formatRiels,
  formatUsd,
  hasReferralBill,
  isUnattributedBill,
  matchesReferralBill,
  summarizeReferralBills,
  UNASSIGNED_PAYER_KEY,
  type PnMemberBillGroup,
  type ReferralBillLookup,
  type ReimbursementStatusFilter,
} from '@/utils/referralBills'

useHead({ title: "PN Referral Bills · Sa'Bai" })

const { data: referrals, isLoading, isError, error } = useReferralsList()
const { data: patientsData } = usePatientsList()
const { data: partners } = useReferralPartnersList()
const { data: staff } = useStaffUsersList()
const { villageCodes } = useVillageCode()

const updateMoney = useUpdateReferralMoney()
const updateReimbursement = useUpdateReferralReimbursement()
const reimburseBills = useReimburseReferralBills()

const filters = useState('referrals:bills-filters', () => ({
  search: '',
  memberWhoPaidId: '',
  reimbursementStatus: 'all' as ReimbursementStatusFilter,
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

const allBills = computed<ReferralListItem[]>(() =>
  (referrals.value ?? [])
    .filter(hasReferralBill)
    .map((item) => {
      const patient = patientsById.value.get(item.patientId)
      const tags = item.tags?.length ? item.tags : (patient?.tags ?? [])
      return { ...item, tags, patientDisplayId: patient?.displayId }
    }),
)

const summary = computed(() => summarizeReferralBills(allBills.value))

const pnGroups = computed(() =>
  consolidateBillsByPn(allBills.value, id => lookup.memberName(id)),
)

const pnFilterOptions = computed(() => {
  const seen = new Map<string, string>()
  for (const u of staff.value ?? []) {
    if (u.roles.some(r => r.trim().toLowerCase() === 'pn'))
      seen.set(u.id, u.nickname || u.userName)
  }
  for (const b of allBills.value) {
    if (b.memberWhoPaidId && !seen.has(b.memberWhoPaidId))
      seen.set(b.memberWhoPaidId, lookup.memberName(b.memberWhoPaidId) ?? 'Unknown PN member')
  }
  return [...seen.entries()]
    .map(([value, label]) => ({ value, label }))
    .sort((a, b) => a.label.localeCompare(b.label))
})

const filteredBills = computed(() =>
  allBills.value.filter(item =>
    matchesReferralBill(
      item,
      {
        search: filters.value.search,
        memberWhoPaidId: filters.value.memberWhoPaidId || null,
        reimbursementStatus: filters.value.reimbursementStatus,
        referredToId: filters.value.referredToId || null,
        villageCodeId: filters.value.villageId || null,
        dateFrom: filters.value.dateFrom || null,
        dateTo: filters.value.dateTo || null,
      },
      lookup,
    ),
  ),
)

const filteredSummary = computed(() => summarizeReferralBills(filteredBills.value))

const hasFilters = computed(
  () =>
    !!filters.value.search
    || !!filters.value.memberWhoPaidId
    || filters.value.reimbursementStatus !== 'all'
    || !!filters.value.referredToId
    || !!filters.value.villageId
    || !!filters.value.dateFrom
    || !!filters.value.dateTo,
)

function clearFilters() {
  filters.value = {
    search: '',
    memberWhoPaidId: '',
    reimbursementStatus: 'all',
    referredToId: '',
    villageId: '',
    dateFrom: '',
    dateTo: '',
  }
}

function formatSummaryTotals(usd: number, riels: number): string {
  const parts: string[] = []
  if (usd > 0) parts.push(formatUsd(usd))
  if (riels > 0) parts.push(formatRiels(riels))
  return parts.length ? parts.join(' + ') : 'US$0.00'
}

const summaryCells = computed(() => [
  {
    key: 'all' as const,
    label: 'All bills',
    count: summary.value.totalCount,
    detail: formatSummaryTotals(summary.value.totalUsd, summary.value.totalRiels),
  },
  {
    key: 'pending' as const,
    label: 'Pending reimbursement',
    count: summary.value.pendingCount,
    detail: formatSummaryTotals(summary.value.pendingUsd, summary.value.pendingRiels),
  },
  {
    key: 'reimbursed' as const,
    label: 'Reimbursed',
    count: summary.value.reimbursedCount,
    detail: formatSummaryTotals(summary.value.reimbursedUsd, summary.value.reimbursedRiels),
  },
  {
    key: 'unattributed' as const,
    label: 'Missing payer',
    count: summary.value.unattributedCount,
    detail: summary.value.unattributedCount > 0
      ? formatSummaryTotals(summary.value.unattributedUsd, summary.value.unattributedRiels)
      : 'All attributed',
  },
])

// ── Bulk selection & reimbursement ───────────────────────────────────────
const selectedIds = ref<string[]>([])
const visibleIds = computed(() => filteredBills.value.map(b => b.id))
const allVisibleSelected = computed(
  () => visibleIds.value.length > 0 && visibleIds.value.every(id => selectedIds.value.includes(id)),
)

watch(filteredBills, (bills) => {
  const valid = new Set(bills.map(b => b.id))
  selectedIds.value = selectedIds.value.filter(id => valid.has(id))
})

function toggleSelectAll(checked: boolean | 'indeterminate') {
  selectedIds.value = checked === true ? [...visibleIds.value] : []
}

function toggleRowSelected(id: string, checked: boolean | 'indeterminate') {
  if (checked === true) {
    if (!selectedIds.value.includes(id))
      selectedIds.value = [...selectedIds.value, id]
  }
  else {
    selectedIds.value = selectedIds.value.filter(x => x !== id)
  }
}

// ── Settle / bulk reimburse dialog ───────────────────────────────────────
const settleDialog = ref<{
  open: boolean
  ids: string[]
  isReimbursed: boolean
  title: string
  description: string
  notes: string
}>({
  open: false,
  ids: [],
  isReimbursed: true,
  title: '',
  description: '',
  notes: '',
})

function openSettleForPn(group: PnMemberBillGroup) {
  if (group.pendingIds.length === 0) return
  settleDialog.value = {
    open: true,
    ids: [...group.pendingIds],
    isReimbursed: true,
    title: `Reimburse ${group.memberName}`,
    description: `Mark ${group.pendingCount} pending bill${group.pendingCount === 1 ? '' : 's'} (${formatSummaryTotals(group.pendingUsd, group.pendingRiels)}) as reimbursed.`,
    notes: '',
  }
}

function openBulkSettle(isReimbursed: boolean) {
  if (selectedIds.value.length === 0) return
  const count = selectedIds.value.length
  settleDialog.value = {
    open: true,
    ids: [...selectedIds.value],
    isReimbursed,
    title: isReimbursed ? `Mark ${count} bill${count === 1 ? '' : 's'} reimbursed` : `Mark ${count} bill${count === 1 ? '' : 's'} pending`,
    description: isReimbursed
      ? `Confirm reimbursement for ${count} selected referral bill${count === 1 ? '' : 's'}.`
      : `Return ${count} selected referral bill${count === 1 ? '' : 's'} to pending reimbursement.`,
    notes: '',
  }
}

async function confirmSettle() {
  if (reimburseBills.isPending.value || settleDialog.value.ids.length === 0) return
  try {
    const outcome = await reimburseBills.mutateAsync({
      referralIds: settleDialog.value.ids,
      isReimbursed: settleDialog.value.isReimbursed,
      reimbursementNotes: settleDialog.value.notes.trim() || null,
      label: settleDialog.value.title,
    })
    notifySaved(
      outcome,
      settleDialog.value.isReimbursed ? 'Bills marked as reimbursed' : 'Bills marked as pending',
    )
    selectedIds.value = []
    settleDialog.value.open = false
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Could not update reimbursement status.'))
  }
}

async function toggleSingleReimbursement(item: ReferralListItem) {
  if (updateReimbursement.isPending.value) return
  const next = !item.isReimbursed
  try {
    const outcome = await updateReimbursement.mutateAsync({
      id: item.id,
      patientName: item.patientName,
      isReimbursed: next,
      reimbursementNotes: next ? item.reimbursementNotes : null,
    })
    notifySaved(outcome, next ? 'Marked as reimbursed' : 'Marked as pending')
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Could not update reimbursement status.'))
  }
}

// ── Edit bill dialog ─────────────────────────────────────────────────────
const editDialog = ref<{
  open: boolean
  item: ReferralListItem | null
  amountPaidUsd: string
  amountPaidRiels: string
  memberWhoPaidId: string
  isReimbursed: boolean
  reimbursementNotes: string
  errors: ReferralMoneyErrors
}>({
  open: false,
  item: null,
  amountPaidUsd: '',
  amountPaidRiels: '',
  memberWhoPaidId: '',
  isReimbursed: false,
  reimbursementNotes: '',
  errors: {},
})

const editPnOptions = usePnMemberOptions(() => editDialog.value.item?.memberWhoPaidId ?? '')

function openEditBill(item: ReferralListItem) {
  editDialog.value = {
    open: true,
    item,
    amountPaidUsd: item.amountPaidUsd != null ? String(item.amountPaidUsd) : '',
    amountPaidRiels: item.amountPaidRiels != null ? String(item.amountPaidRiels) : '',
    memberWhoPaidId: item.memberWhoPaidId ?? '',
    isReimbursed: item.isReimbursed,
    reimbursementNotes: item.reimbursementNotes ?? '',
    errors: {},
  }
}

async function saveEditBill() {
  const item = editDialog.value.item
  if (!item || updateMoney.isPending.value || updateReimbursement.isPending.value) return

  const errors = validateReferralMoney({
    amountPaidUsd: editDialog.value.amountPaidUsd,
    amountPaidRiels: editDialog.value.amountPaidRiels,
    memberWhoPaidId: editDialog.value.memberWhoPaidId,
  })
  editDialog.value.errors = errors
  if (Object.keys(errors).length > 0) return

  const usd = editDialog.value.amountPaidUsd.trim()
  const riels = editDialog.value.amountPaidRiels.trim()
  const nextUsd = usd ? Number(usd) : null
  const nextRiels = riels ? Number(riels) : null
  const nextMember = editDialog.value.memberWhoPaidId || null
  const nextReimbursed = editDialog.value.isReimbursed
  const nextNotes = editDialog.value.reimbursementNotes.trim() || null

  const moneyChanged
    = nextUsd !== item.amountPaidUsd
    || nextRiels !== item.amountPaidRiels
    || nextMember !== item.memberWhoPaidId

  const reimbursementChanged
    = nextReimbursed !== item.isReimbursed
    || nextNotes !== (item.reimbursementNotes ?? null)

  try {
    let outcome
    if (moneyChanged) {
      outcome = await updateMoney.mutateAsync({
        id: item.id,
        patientName: item.patientName,
        amountPaidUsd: nextUsd,
        amountPaidRiels: nextRiels,
        memberWhoPaidId: nextMember,
      })
    }
    if (reimbursementChanged) {
      outcome = await updateReimbursement.mutateAsync({
        id: item.id,
        patientName: item.patientName,
        isReimbursed: nextReimbursed,
        reimbursementNotes: nextNotes,
      })
    }
    if (outcome)
      notifySaved(outcome, 'Bill updated')
    editDialog.value.open = false
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Could not save bill changes.'))
  }
}

// ── CSV export ───────────────────────────────────────────────────────────
function exportCsv() {
  if (filteredBills.value.length === 0) return
  const csv = buildReferralBillsCsv(filteredBills.value, lookup)
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  const today = new Date().toISOString().slice(0, 10)
  link.href = url
  link.download = `sabai-referral-bills-${today}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

const shortDate = (date: string) => {
  const d = new Date(date)
  return d.toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: d.getFullYear() === new Date().getFullYear() ? undefined : 'numeric',
  })
}
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      title="PN Referral Bills"
      description="Consolidate, review, and reimburse referral bills paid by PN members."
      :breadcrumbs="[
        { label: 'Home', to: '/' },
        { label: 'Referrals', to: paths.referrals() },
        { label: 'PN Bills' },
      ]"
      class="mb-0!"
    >
      <template #actions>
        <Button
          variant="outline"
          :disabled="isLoading || filteredBills.length === 0"
          @click="exportCsv"
        >
          <PhDownloadSimple aria-hidden="true" /> Export CSV
        </Button>
        <Button as-child variant="outline">
          <NuxtLink :to="paths.referrals()">
            <PhArrowLeft aria-hidden="true" /> All referrals
          </NuxtLink>
        </Button>
      </template>
    </PageHeader>

    <!-- Summary strip: clickable to filter by reimbursement status -->
    <div role="group" aria-label="Filter by bill status" class="grid grid-cols-2 gap-px border border-border bg-border sm:grid-cols-4">
      <button
        v-for="cell in summaryCells"
        :key="cell.key"
        type="button"
        class="flex min-w-0 flex-col gap-0.5 bg-card px-3 py-2 text-left hover:bg-accent focus-visible:bg-accent focus-visible:outline-none"
        :class="filters.reimbursementStatus === cell.key && 'bg-accent shadow-[inset_0_-3px_0_var(--color-primary)]'"
        :aria-pressed="filters.reimbursementStatus === cell.key"
        @click="filters.reimbursementStatus = cell.key"
      >
        <span class="flex items-baseline justify-between gap-2">
          <span class="truncate text-xs font-bold text-chrome touch:text-sm">{{ cell.label }}</span>
          <span
            class="text-lg leading-tight font-bold tabular-nums"
            :class="cell.count === 0 ? 'text-muted-foreground' : cell.key === 'unattributed' ? 'text-warning' : ''"
          >
            {{ isLoading ? '–' : cell.count }}
          </span>
        </span>
        <span class="truncate text-xs font-medium text-muted-foreground tabular-nums touch:text-[0.8125rem]">
          {{ isLoading ? '–' : cell.detail }}
        </span>
      </button>
    </div>

    <!-- Consolidation by PN Member -->
    <AppPanel
      title="Consolidation by PN member"
      :description="isLoading ? undefined : `${pnGroups.length} ${pnGroups.length === 1 ? 'payer' : 'payers'}`"
      flush
    >
      <StateMessage v-if="isLoading" kind="loading" message="Loading referral bills…" />
      <StateMessage v-else-if="isError && !referrals" kind="error" :message="describeLoadError(error, 'Referral bills could not be loaded. Check the connection and reload.')" />
      <div v-else-if="pnGroups.length === 0" class="px-3 py-8 text-center text-xs text-muted-foreground touch:text-sm" role="status">
        No referral payments recorded yet. Record payments on a referral's Payment step.
      </div>
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>PN Member</TableHead>
            <TableHead>Bills</TableHead>
            <TableHead>Pending</TableHead>
            <TableHead>Reimbursed</TableHead>
            <TableHead>Total Paid</TableHead>
            <TableHead class="text-right">
              Actions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow
            v-for="group in pnGroups"
            :key="group.memberId"
            :class="filters.memberWhoPaidId === group.memberId && 'bg-accent/50'"
          >
            <TableCell data-label="PN Member" class="font-semibold">
              <span class="flex items-center gap-1.5">
                <PhWarning v-if="group.isUnassigned" class="size-4 shrink-0 text-warning" aria-hidden="true" />
                <span>{{ group.memberName }}</span>
              </span>
            </TableCell>
            <TableCell data-label="Bills" class="tabular-nums">
              {{ group.totalCount }}
              <span v-if="group.pendingCount > 0" class="text-xs text-warning">
                ({{ group.pendingCount }} pending)
              </span>
            </TableCell>
            <TableCell data-label="Pending" class="font-medium tabular-nums" :class="group.pendingCount > 0 ? 'text-warning' : 'text-muted-foreground'">
              {{ group.pendingCount > 0 ? formatSummaryTotals(group.pendingUsd, group.pendingRiels) : '—' }}
            </TableCell>
            <TableCell data-label="Reimbursed" class="tabular-nums text-success">
              {{ group.reimbursedCount > 0 ? formatSummaryTotals(group.reimbursedUsd, group.reimbursedRiels) : '—' }}
            </TableCell>
            <TableCell data-label="Total Paid" class="font-bold tabular-nums">
              {{ formatSummaryTotals(group.totalUsd, group.totalRiels) }}
            </TableCell>
            <TableCell data-label="Actions" class="text-right">
              <div class="flex flex-wrap justify-end gap-1.5">
                <Button
                  size="sm"
                  :variant="filters.memberWhoPaidId === group.memberId ? 'default' : 'outline'"
                  @click="filters.memberWhoPaidId = filters.memberWhoPaidId === group.memberId ? '' : group.memberId"
                >
                  {{ filters.memberWhoPaidId === group.memberId ? 'Showing' : 'Filter' }}
                </Button>
                <Button
                  v-if="group.pendingCount > 0 && !group.isUnassigned"
                  size="sm"
                  variant="success"
                  @click="openSettleForPn(group)"
                >
                  <PhCheck aria-hidden="true" /> Settle pending
                </Button>
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </AppPanel>

    <!-- Detailed Bills List -->
    <AppPanel
      title="Referral bills"
      :description="isLoading ? undefined : `${filteredBills.length} shown · ${formatSummaryTotals(filteredSummary.totalUsd, filteredSummary.totalRiels)}`"
      flush
    >
      <template #actions>
        <div class="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:flex-wrap sm:items-center">
          <div class="relative sm:w-56">
            <PhMagnifyingGlass class="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              v-model="filters.search"
              type="search"
              placeholder="Patient, clinic, PN, notes"
              class="bg-card pr-8 pl-8"
              aria-label="Search referral bills"
            />
            <button
              v-if="filters.search"
              type="button"
              class="absolute top-1/2 right-1.5 -translate-y-1/2 p-1 text-muted-foreground hover:text-foreground"
              aria-label="Clear search"
              @click="filters.search = ''"
            >
              <PhX class="size-3.5" aria-hidden="true" />
            </button>
          </div>
          <NativeSelect v-model="filters.memberWhoPaidId" aria-label="PN member who paid" class="bg-card sm:w-44">
            <NativeSelectOption value="">
              All PN members
            </NativeSelectOption>
            <NativeSelectOption :value="UNASSIGNED_PAYER_KEY">
              Unassigned (missing payer)
            </NativeSelectOption>
            <NativeSelectOption v-for="opt in pnFilterOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </NativeSelectOption>
          </NativeSelect>
          <NativeSelect v-model="filters.reimbursementStatus" aria-label="Reimbursement status" class="bg-card sm:w-40">
            <NativeSelectOption value="all">
              All statuses
            </NativeSelectOption>
            <NativeSelectOption value="pending">
              Pending
            </NativeSelectOption>
            <NativeSelectOption value="reimbursed">
              Reimbursed
            </NativeSelectOption>
            <NativeSelectOption value="unattributed">
              Missing payer
            </NativeSelectOption>
          </NativeSelect>
          <NativeSelect v-model="filters.referredToId" aria-label="Partner clinic" class="bg-card sm:w-40">
            <NativeSelectOption value="">
              All clinics
            </NativeSelectOption>
            <NativeSelectOption v-for="p in partners" :key="p.id" :value="p.id">
              {{ p.name }}
            </NativeSelectOption>
          </NativeSelect>
          <NativeSelect v-model="filters.villageId" aria-label="Village" class="bg-card sm:w-36">
            <NativeSelectOption value="">
              All villages
            </NativeSelectOption>
            <NativeSelectOption v-for="v in villageCodes" :key="v.id" :value="v.id">
              {{ v.name }} ({{ v.code }})
            </NativeSelectOption>
          </NativeSelect>
          <Input
            v-model="filters.dateFrom"
            type="date"
            aria-label="From date"
            class="bg-card sm:w-36"
          />
          <Input
            v-model="filters.dateTo"
            type="date"
            aria-label="To date"
            class="bg-card sm:w-36"
          />
          <Button
            v-if="hasFilters"
            variant="ghost"
            size="sm"
            class="text-xs touch:h-10"
            @click="clearFilters"
          >
            Reset
          </Button>
        </div>
      </template>

      <!-- Bulk selection bar -->
      <div
        v-if="selectedIds.length > 0"
        class="flex flex-wrap items-center justify-between gap-2 border-b border-border bg-accent/60 px-3 py-2"
      >
        <span class="text-xs font-semibold text-chrome touch:text-sm">
          {{ selectedIds.length }} bill{{ selectedIds.length === 1 ? '' : 's' }} selected
        </span>
        <div class="flex flex-wrap items-center gap-2">
          <Button size="sm" variant="success" @click="openBulkSettle(true)">
            <PhCheckCircle aria-hidden="true" /> Mark reimbursed
          </Button>
          <Button size="sm" variant="outline" @click="openBulkSettle(false)">
            Mark pending
          </Button>
          <Button size="sm" variant="ghost" @click="selectedIds = []">
            Clear
          </Button>
        </div>
      </div>

      <div v-if="!isLoading && filteredBills.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
        <p class="text-xs text-muted-foreground touch:text-sm">
          {{ allBills.length === 0 ? 'No referral bills recorded yet.' : 'No referral bills match these filters.' }}
        </p>
        <Button v-if="hasFilters" variant="outline" size="sm" @click="clearFilters">
          Clear filters
        </Button>
      </div>

      <Table v-else-if="filteredBills.length > 0" class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead class="w-10">
              <Checkbox
                :model-value="allVisibleSelected"
                aria-label="Select all visible bills"
                @update:model-value="toggleSelectAll"
              />
            </TableHead>
            <TableHead>Patient</TableHead>
            <TableHead>Referral & Clinic</TableHead>
            <TableHead>PN Member</TableHead>
            <TableHead>Amount</TableHead>
            <TableHead>Reimbursement</TableHead>
            <TableHead class="text-right">
              Actions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="bill in filteredBills" :key="bill.id">
            <TableCell data-label="Select">
              <Checkbox
                :model-value="selectedIds.includes(bill.id)"
                :aria-label="`Select bill for ${bill.patientName}`"
                @update:model-value="(val) => toggleRowSelected(bill.id, val)"
              />
            </TableCell>
            <TableCell data-label="Patient">
              <NuxtLink :to="paths.referralsDetail(bill.id)" class="font-semibold text-primary hover:underline">
                {{ bill.patientName }}
              </NuxtLink>
              <div class="mt-0.5 flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
                <PatientCode
                  :id="bill.patientId"
                  :village-code="bill.villageCode"
                  :village-color-hex="bill.villageColorHex"
                  :display-id="bill.patientDisplayId ?? bill.villageCode"
                />
                <span class="tabular-nums">{{ shortDate(bill.createdAt) }}</span>
              </div>
            </TableCell>
            <TableCell data-label="Referral & Clinic">
              <p class="font-medium text-chrome">
                {{ bill.referredFor }}
              </p>
              <p class="text-xs text-muted-foreground">
                {{ lookup.partnerName(bill.referredToId) || 'No partner clinic' }}
              </p>
            </TableCell>
            <TableCell data-label="PN Member">
              <span v-if="bill.memberWhoPaidId" class="font-medium">
                {{ lookup.memberName(bill.memberWhoPaidId) ?? 'Unknown PN member' }}
              </span>
              <Badge v-else-if="isUnattributedBill(bill)" variant="outline" class="border-warning-border bg-warning-subtle text-warning">
                Missing payer
              </Badge>
              <span v-else class="text-muted-foreground">—</span>
            </TableCell>
            <TableCell data-label="Amount" class="font-bold tabular-nums">
              {{ formatBillAmount(bill.amountPaidUsd, bill.amountPaidRiels) }}
            </TableCell>
            <TableCell data-label="Reimbursement">
              <div class="flex flex-col gap-0.5">
                <Badge
                  v-if="bill.isReimbursed"
                  variant="outline"
                  class="w-fit border-success/40 bg-success-subtle text-success"
                >
                  Reimbursed
                </Badge>
                <Badge
                  v-else
                  variant="outline"
                  class="w-fit border-warning-border bg-warning-subtle text-warning"
                >
                  Pending
                </Badge>
                <span v-if="bill.reimbursedAt" class="text-xs text-muted-foreground tabular-nums">
                  {{ shortDate(bill.reimbursedAt) }}
                </span>
                <span v-if="bill.reimbursementNotes" class="line-clamp-1 text-xs text-muted-foreground" :title="bill.reimbursementNotes">
                  {{ bill.reimbursementNotes }}
                </span>
              </div>
            </TableCell>
            <TableCell data-label="Actions" class="text-right">
              <div class="flex flex-wrap justify-end gap-1.5">
                <Button
                  size="sm"
                  :variant="bill.isReimbursed ? 'outline' : 'success'"
                  :disabled="updateReimbursement.isPending.value"
                  @click="toggleSingleReimbursement(bill)"
                >
                  {{ bill.isReimbursed ? 'Mark pending' : 'Reimburse' }}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  aria-label="Edit payment"
                  @click="openEditBill(bill)"
                >
                  <PhPencilSimple aria-hidden="true" /> Edit
                </Button>
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </AppPanel>

    <!-- Settle / Bulk Reimburse Dialog -->
    <Dialog v-model:open="settleDialog.open">
      <DialogContent class="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{{ settleDialog.title }}</DialogTitle>
          <DialogDescription>{{ settleDialog.description }}</DialogDescription>
        </DialogHeader>
        <div class="flex flex-col gap-3 py-2">
          <FormField v-slot="{ id }" label="Reimbursement note (optional)">
            <Textarea
              :id="id"
              v-model="settleDialog.notes"
              :rows="2"
              placeholder="e.g. Cash reimbursed on 14 Nov, receipt #12"
            />
          </FormField>
        </div>
        <DialogFooter>
          <Button variant="outline" :disabled="reimburseBills.isPending.value" @click="settleDialog.open = false">
            Cancel
          </Button>
          <Button
            :variant="settleDialog.isReimbursed ? 'success' : 'default'"
            :disabled="reimburseBills.isPending.value"
            @click="confirmSettle"
          >
            {{ reimburseBills.isPending.value ? 'Saving…' : 'Confirm' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Edit Bill Dialog -->
    <Dialog v-model:open="editDialog.open">
      <DialogContent class="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Edit referral bill</DialogTitle>
          <DialogDescription v-if="editDialog.item">
            {{ editDialog.item.patientName }} · {{ editDialog.item.referredFor }}
          </DialogDescription>
        </DialogHeader>
        <div class="flex flex-col gap-3 py-2">
          <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <FormField v-slot="{ id, describedby, invalid }" label="Paid in US dollars" :error="editDialog.errors.amountPaidUsd">
              <NumericInput
                :id="id"
                v-model="editDialog.amountPaidUsd"
                unit="USD"
                step="0.01"
                min="0"
                placeholder="0.00"
                :aria-describedby="describedby"
                :aria-invalid="invalid || undefined"
              />
            </FormField>
            <FormField v-slot="{ id, describedby, invalid }" label="Paid in riels" :error="editDialog.errors.amountPaidRiels">
              <NumericInput
                :id="id"
                v-model="editDialog.amountPaidRiels"
                unit="KHR"
                step="1"
                min="0"
                placeholder="0"
                :aria-describedby="describedby"
                :aria-invalid="invalid || undefined"
              />
            </FormField>
          </div>
          <FormField v-slot="{ id }" label="PN member who paid">
            <NativeSelect :id="id" v-model="editDialog.memberWhoPaidId">
              <NativeSelectOption value="">
                Not chosen
              </NativeSelectOption>
              <NativeSelectOption v-for="opt in editPnOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </NativeSelectOption>
            </NativeSelect>
          </FormField>
          <div class="flex items-center gap-2 pt-1">
            <Checkbox id="edit-bill-reimbursed" v-model="editDialog.isReimbursed" />
            <label for="edit-bill-reimbursed" class="text-sm font-medium">
              Reimbursed to PN member
            </label>
          </div>
          <FormField v-slot="{ id }" label="Reimbursement note (optional)">
            <Textarea
              :id="id"
              v-model="editDialog.reimbursementNotes"
              :rows="2"
              placeholder="e.g. Settled in cash"
            />
          </FormField>
        </div>
        <DialogFooter>
          <Button
            variant="outline"
            :disabled="updateMoney.isPending.value || updateReimbursement.isPending.value"
            @click="editDialog.open = false"
          >
            Cancel
          </Button>
          <Button
            variant="success"
            :disabled="updateMoney.isPending.value || updateReimbursement.isPending.value"
            @click="saveEditBill"
          >
            {{ updateMoney.isPending.value || updateReimbursement.isPending.value ? 'Saving…' : 'Save changes' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
