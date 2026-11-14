<script setup lang="ts">
import { PhCaretDown, PhCaretRight, PhX } from '@phosphor-icons/vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

definePageMeta({ requiresAdmin: true })
useHead({ title: "Audit Log · Sa'Bai" })

const route = useRoute()
const fromQuery = (key: string) => (typeof route.query[key] === 'string' ? route.query[key] : '')

// Filters start from the URL so a record's history can be linked to directly,
// e.g. /settings/audit-log?entityType=Patient&entityId=12.
const action = ref<AuditAction | ''>(fromQuery('action') as AuditAction | '')
const userId = ref(fromQuery('userId'))
const entityType = ref(fromQuery('entityType'))
const entityId = ref(fromQuery('entityId'))
const fromDate = ref(fromQuery('from'))
const toDate = ref(fromQuery('to'))
const debouncedType = useDebouncedRef(entityType, 300)
const debouncedId = useDebouncedRef(entityId, 300)

watch(
  () => route.query,
  () => {
    action.value = fromQuery('action') as AuditAction | ''
    userId.value = fromQuery('userId')
    entityType.value = fromQuery('entityType')
    entityId.value = fromQuery('entityId')
    fromDate.value = fromQuery('from')
    toDate.value = fromQuery('to')
  },
)

const filters = computed<AuditLogFilters>(() => ({
  action: action.value || undefined,
  userId: userId.value || undefined,
  entityType: debouncedType.value,
  entityId: debouncedId.value,
  fromDate: fromDate.value || undefined,
  toDate: toDate.value || undefined,
}))
const hasFilters = computed(() =>
  [action.value, userId.value, entityType.value, entityId.value, fromDate.value, toDate.value].some(Boolean),
)
function clearFilters() {
  action.value = ''
  userId.value = ''
  entityType.value = ''
  entityId.value = ''
  fromDate.value = ''
  toDate.value = ''
}

const { data, isLoading, isError, error, hasNextPage, fetchNextPage, isFetchingNextPage } = useAuditLog(filters)
const entries = computed(() => data.value?.pages.flatMap(p => p.entries) ?? [])
const { data: staff } = useStaffUsersList()

const expanded = ref(new Set<string>())
function toggle(id: string) {
  const next = new Set(expanded.value)
  if (!next.delete(id))
    next.add(id)
  expanded.value = next
}

const actionClass: Record<AuditAction, string> = {
  Created: 'border-success/40 bg-success-subtle text-success',
  Updated: 'border-chrome/40 bg-band text-chrome',
  Deleted: 'border-destructive/40 bg-destructive-subtle text-destructive',
  SignedIn: '',
  SignInFailed: 'border-warning-border bg-warning-subtle text-warning',
  SignedOut: '',
  Read: 'border-chrome/40 bg-band text-chrome',
  Exported: 'border-warning-border bg-warning-subtle text-warning',
  Disclosed: 'border-warning-border bg-warning-subtle text-warning',
}

const timeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' })
function formatTime(iso: string) {
  return timeFormat.format(new Date(iso))
}

function subject(entry: AuditLogEntry) {
  if (!entry.entityType)
    return entry.details ?? ''
  return entry.entityId
    ? `${humanizeName(entry.entityType)} #${entry.entityId}`
    : humanizeName(entry.entityType)
}

/** Short "Name, Status" list of what changed, for the collapsed row. */
function summary(entry: AuditLogEntry) {
  if (PHI_ACCESS_ACTIONS.includes(entry.action))
    return entry.request ?? ''
  if (entry.action !== 'Updated')
    return ''
  const fields = entry.changes.map(c => humanizeName(c.field))
  return fields.length > 4 ? `${fields.slice(0, 4).join(', ')} +${fields.length - 4} more` : fields.join(', ')
}

/** The page showing this record, when there is one to open. */
function recordLink(entry: AuditLogEntry) {
  const type = entry.entityType
  if (!type)
    return undefined
  if (type === 'Patient') {
    const id = entry.entityId
    return entry.action !== 'Deleted' && isUuid(id) ? paths.patientDetail(id) : undefined
  }
  if (type === 'Visit' || type === 'ClinicalFile') {
    const patientField = entry.changes.find(c => c.field === 'PatientId')
    const patientId = patientField?.new ?? patientField?.old
    return isUuid(patientId) ? paths.patientDetail(patientId) : undefined
  }
  if (type === 'MedicationStock' || type === 'StockChange')
    return paths.medicationStock()
  if (type === 'MedicationBrand')
    return paths.medicationBrands()
  if (type === 'MedicationActiveIngredient')
    return paths.medicationActiveIngredients()
  if (type === 'VillageCode')
    return paths.settingsVillageCodes()
  if (type === 'StaffUser' || type === 'UserRole' || type === 'UserToken')
    return paths.settingsUsers()
  if (type === 'Order' || type === 'OrderItem')
    return paths.orders()
  return undefined
}

function filterByEntity(entry: AuditLogEntry) {
  entityType.value = entry.entityType ?? ''
  entityId.value = entry.entityId ?? ''
}
</script>

<template>
  <div>
    <PageHeader
      title="Audit Log"
      description="Every record created, changed or deleted, staff sign-ins, and who viewed, exported or disclosed patient data. Newest first."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings', to: paths.settings() }, { label: 'Audit Log' }]"
    />

    <AppPanel title="Filters" class="mb-4">
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-6">
        <FormField v-slot="{ id }" label="Action">
          <NativeSelect :id="id" v-model="action" class="w-full">
            <NativeSelectOption value="">
              Any action
            </NativeSelectOption>
            <NativeSelectOption v-for="(label, key) in AUDIT_ACTION_LABELS" :key="key" :value="key">
              {{ label }}
            </NativeSelectOption>
          </NativeSelect>
        </FormField>
        <FormField v-slot="{ id }" label="Staff user">
          <NativeSelect :id="id" v-model="userId" class="w-full">
            <NativeSelectOption value="">
              Anyone
            </NativeSelectOption>
            <NativeSelectOption v-for="u in staff ?? []" :key="u.id" :value="u.id">
              {{ u.nickname }} ({{ u.userName }})
            </NativeSelectOption>
          </NativeSelect>
        </FormField>
        <FormField v-slot="{ id }" label="Record type" hint="e.g. Patient, Consult">
          <Input :id="id" v-model="entityType" placeholder="Any" />
        </FormField>
        <FormField v-slot="{ id }" label="Record id">
          <Input :id="id" v-model="entityId" placeholder="Any" />
        </FormField>
        <FormField v-slot="{ id }" label="From">
          <Input :id="id" v-model="fromDate" type="date" />
        </FormField>
        <FormField v-slot="{ id }" label="To">
          <Input :id="id" v-model="toDate" type="date" :min="fromDate || undefined" />
        </FormField>
      </div>
      <div v-if="hasFilters" class="mt-3 flex justify-end">
        <Button size="sm" variant="outline" class="max-sm:w-full" @click="clearFilters">
          <PhX aria-hidden="true" /> Clear filters
        </Button>
      </div>
    </AppPanel>

    <AppPanel title="Entries" flush>
      <StateMessage v-if="isLoading" kind="loading" message="Loading audit log..." />
      <StateMessage v-else-if="isError" kind="error" :message="describeLoadError(error, 'Unable to load the audit log.')" />
      <StateMessage v-else-if="entries.length === 0" :message="hasFilters ? 'No entries match these filters.' : 'Nothing has been recorded yet.'" />
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead class="w-8">
              <span class="sr-only">Details</span>
            </TableHead>
            <TableHead>When</TableHead>
            <TableHead>Who</TableHead>
            <TableHead>Action</TableHead>
            <TableHead>Record</TableHead>
            <TableHead>Changed / accessed</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-for="entry in entries" :key="entry.id">
            <TableRow
              class="cursor-pointer"
              :class="expanded.has(entry.id) && 'border-b-0 md:border-b bg-muted/20'"
              @click="toggle(entry.id)"
            >
              <TableCell data-stack="title" class="md:w-8">
                <!-- Desktop caret toggle button -->
                <Button
                  size="icon-sm"
                  variant="ghost"
                  :aria-expanded="expanded.has(entry.id)"
                  :aria-label="`${expanded.has(entry.id) ? 'Hide' : 'Show'} details for entry ${entry.id}`"
                  class="hidden md:inline-flex"
                  @click.stop="toggle(entry.id)"
                >
                  <PhCaretDown v-if="expanded.has(entry.id)" aria-hidden="true" />
                  <PhCaretRight v-else aria-hidden="true" />
                </Button>

                <!-- Mobile card header: Action badge + Record/Subject title + Caret -->
                <div class="flex items-center justify-between gap-2 md:hidden">
                  <div class="flex items-center gap-2 min-w-0">
                    <Badge variant="outline" :class="actionClass[entry.action]">
                      {{ AUDIT_ACTION_LABELS[entry.action] }}
                    </Badge>
                    <span class="truncate font-semibold text-foreground text-sm touch:text-base">
                      {{ subject(entry) }}
                    </span>
                  </div>
                  <Button
                    size="icon-sm"
                    variant="ghost"
                    :aria-expanded="expanded.has(entry.id)"
                    :aria-label="`${expanded.has(entry.id) ? 'Hide' : 'Show'} details for entry ${entry.id}`"
                    class="shrink-0 text-muted-foreground"
                    @click.stop="toggle(entry.id)"
                  >
                    <PhCaretDown v-if="expanded.has(entry.id)" class="size-4" aria-hidden="true" />
                    <PhCaretRight v-else class="size-4" aria-hidden="true" />
                  </Button>
                </div>
              </TableCell>

              <TableCell data-label="When" class="whitespace-nowrap tabular-nums">
                {{ formatTime(entry.occurredAt) }}
              </TableCell>

              <TableCell data-label="Who">
                <button
                  v-if="entry.userName && entry.userId"
                  type="button"
                  class="text-left font-mono underline-offset-2 hover:underline"
                  title="Show only this user's activity"
                  @click.stop="userId = entry.userId"
                >
                  {{ entry.userName }}
                </button>
                <span v-else-if="entry.userName" class="font-mono">{{ entry.userName }}</span>
                <span v-else class="text-muted-foreground">System</span>
              </TableCell>

              <TableCell data-label="Action" class="hidden md:table-cell">
                <Badge variant="outline" :class="actionClass[entry.action]">
                  {{ AUDIT_ACTION_LABELS[entry.action] }}
                </Badge>
              </TableCell>

              <TableCell
                data-label="Record"
                :class="[!entry.entityType && !recordLink(entry) && 'hidden md:table-cell', 'max-w-80 break-all']"
              >
                <button
                  v-if="entry.entityType"
                  type="button"
                  class="text-left underline-offset-2 hover:underline"
                  :title="`Show only this record's history`"
                  @click.stop="filterByEntity(entry)"
                >
                  {{ subject(entry) }}
                </button>
                <span v-else class="text-muted-foreground">{{ subject(entry) }}</span>
                <NuxtLink
                  v-if="recordLink(entry)"
                  :to="recordLink(entry)!"
                  class="ml-2 text-xs font-semibold whitespace-nowrap text-primary hover:underline touch:text-sm"
                  @click.stop
                >
                  Open record →
                </NuxtLink>
              </TableCell>

              <TableCell
                data-label="Changed / accessed"
                :class="[!summary(entry) && 'hidden md:table-cell', 'text-muted-foreground']"
              >
                {{ summary(entry) }}
              </TableCell>
            </TableRow>

            <TableRow
              v-if="expanded.has(entry.id)"
              data-stack="detail-row"
              class="bg-muted/40 hover:bg-muted/40 border-t-0"
            >
              <TableCell colspan="6" data-stack="detail" class="p-3">
                <dl class="mb-2 grid grid-cols-[auto_1fr] gap-x-4 gap-y-0.5 text-xs touch:text-sm">
                  <dt class="text-muted-foreground">
                    Request
                  </dt>
                  <dd class="font-mono break-all">
                    {{ entry.request ?? '—' }}
                  </dd>
                  <dt class="text-muted-foreground">
                    IP address
                  </dt>
                  <dd class="font-mono">
                    {{ entry.ipAddress ?? '—' }}
                  </dd>
                  <template v-if="entry.details && entry.entityType">
                    <dt class="text-muted-foreground">
                      Details
                    </dt>
                    <dd>{{ entry.details }}</dd>
                  </template>
                </dl>

                <!-- Mobile change list (< 768px) -->
                <div v-if="entry.changes.length" class="space-y-1.5 md:hidden">
                  <div class="text-xs font-bold text-chrome uppercase tracking-wide">
                    Changes
                  </div>
                  <div class="divide-y divide-border border border-border bg-card">
                    <div v-for="change in entry.changes" :key="change.field" class="p-2 text-xs">
                      <div class="font-semibold text-foreground">
                        {{ humanizeName(change.field) }}
                      </div>
                      <div v-if="entry.action !== 'Created'" class="mt-0.5 break-all text-muted-foreground">
                        <span class="font-medium text-muted-foreground/75">Before:</span>
                        <span v-if="change.old === null" class="italic"> empty</span>
                        <span v-else> {{ change.old }}</span>
                      </div>
                      <div v-if="entry.action !== 'Deleted'" class="mt-0.5 break-all text-foreground">
                        <span class="font-medium text-muted-foreground">After:</span>
                        <span v-if="change.new === null" class="text-muted-foreground italic"> empty</span>
                        <span v-else> {{ change.new }}</span>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- Desktop change table (>= 768px) -->
                <table v-if="entry.changes.length" class="hidden md:table w-full border border-border bg-card text-xs touch:text-sm">
                  <thead class="bg-band text-left text-chrome">
                    <tr>
                      <th class="px-2 py-1 font-bold">
                        Field
                      </th>
                      <th v-if="entry.action !== 'Created'" class="px-2 py-1 font-bold">
                        Before
                      </th>
                      <th v-if="entry.action !== 'Deleted'" class="px-2 py-1 font-bold">
                        After
                      </th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-border">
                    <tr v-for="change in entry.changes" :key="change.field" class="align-top">
                      <td class="px-2 py-1 font-semibold whitespace-nowrap">
                        {{ humanizeName(change.field) }}
                      </td>
                      <td v-if="entry.action !== 'Created'" class="px-2 py-1 break-all whitespace-pre-wrap">
                        <span v-if="change.old === null" class="text-muted-foreground">empty</span>
                        <template v-else>
                          {{ change.old }}
                        </template>
                      </td>
                      <td v-if="entry.action !== 'Deleted'" class="px-2 py-1 break-all whitespace-pre-wrap">
                        <span v-if="change.new === null" class="text-muted-foreground">empty</span>
                        <template v-else>
                          {{ change.new }}
                        </template>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </TableCell>
            </TableRow>
          </template>
        </TableBody>
      </Table>
      <div v-if="hasNextPage" class="flex justify-center border-t border-border p-3">
        <Button variant="outline" :disabled="isFetchingNextPage" @click="fetchNextPage()">
          {{ isFetchingNextPage ? 'Loading…' : 'Load older entries' }}
        </Button>
      </div>
    </AppPanel>
  </div>
</template>
