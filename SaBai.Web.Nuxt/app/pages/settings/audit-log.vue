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

const expanded = ref(new Set<number>())
function toggle(id: number) {
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
}

const timeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' })
function formatTime(iso: string) {
  return timeFormat.format(new Date(iso))
}

function subject(entry: AuditLogEntry) {
  if (!entry.entityType)
    return entry.details ?? ''
  return `${humanizeName(entry.entityType)} #${entry.entityId ?? '?'}`
}

/** Short "Name, Status" list of what changed, for the collapsed row. */
function summary(entry: AuditLogEntry) {
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
    const id = Number(entry.entityId)
    return entry.action !== 'Deleted' && Number.isInteger(id) && id > 0 ? paths.patientDetail(id) : undefined
  }
  if (type === 'Visit' || type === 'ClinicalFile') {
    const patientField = entry.changes.find(c => c.field === 'PatientId')
    const patientId = Number(patientField?.new ?? patientField?.old)
    return Number.isInteger(patientId) && patientId > 0 ? paths.patientDetail(patientId) : undefined
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
      description="Every record created, changed or deleted, and staff sign-ins. Newest first."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings', to: paths.settings() }, { label: 'Audit Log' }]"
    />

    <AppPanel title="Filters" class="mb-4">
      <div class="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
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
        <Button size="sm" variant="outline" @click="clearFilters">
          <PhX aria-hidden="true" /> Clear filters
        </Button>
      </div>
    </AppPanel>

    <AppPanel title="Entries" flush>
      <StateMessage v-if="isLoading" kind="loading" message="Loading audit log..." />
      <StateMessage v-else-if="isError" kind="error" :message="getApiErrorMessage(error, 'Unable to load the audit log.')" />
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
            <TableHead>Changed</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <template v-for="entry in entries" :key="entry.id">
            <TableRow class="cursor-pointer" @click="toggle(entry.id)">
              <TableCell data-stack="actions">
                <Button
                  size="icon-sm"
                  variant="ghost"
                  :aria-expanded="expanded.has(entry.id)"
                  :aria-label="`${expanded.has(entry.id) ? 'Hide' : 'Show'} details for entry ${entry.id}`"
                  @click.stop="toggle(entry.id)"
                >
                  <PhCaretDown v-if="expanded.has(entry.id)" aria-hidden="true" />
                  <PhCaretRight v-else aria-hidden="true" />
                </Button>
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
              <TableCell data-label="Action">
                <Badge variant="outline" :class="actionClass[entry.action]">
                  {{ AUDIT_ACTION_LABELS[entry.action] }}
                </Badge>
              </TableCell>
              <TableCell data-label="Record" class="max-w-80 break-all">
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
              <TableCell data-label="Changed" class="text-muted-foreground">
                {{ summary(entry) }}
              </TableCell>
            </TableRow>
            <TableRow v-if="expanded.has(entry.id)" class="bg-muted/40 hover:bg-muted/40">
              <TableCell colspan="6" class="p-3">
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
                <table v-if="entry.changes.length" class="w-full border border-border bg-card text-xs touch:text-sm">
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
