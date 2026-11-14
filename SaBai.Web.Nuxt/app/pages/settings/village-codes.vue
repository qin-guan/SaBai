<script setup lang="ts">
import { PhPencilSimple, PhPlus, PhTrash } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import type { LocalVillageCode } from '~/db/types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Village Codes · Sa'Bai" })

const showHidden = ref(false)
const { data, isLoading } = useVillageCodesList(showHidden)
const codes = computed(() => data.value?.villageCodes ?? [])

const mode = ref<'editing' | 'deleting' | null>(null)
const active = ref<LocalVillageCode | null>(null)

function openEdit(code: LocalVillageCode | null) {
  active.value = code
  mode.value = 'editing'
}
function openDelete(code: LocalVillageCode) {
  active.value = code
  mode.value = 'deleting'
}
function close() {
  mode.value = null
  active.value = null
}

const isDeleting = ref(false)
async function deleteVillageCode(id: string) {
  if (isDeleting.value)
    return
  isDeleting.value = true
  try {
    const db = getPowerSyncInstance()
    const visitRow = await db.getOptional<{ c: number }>(
      'SELECT COUNT(*) AS c FROM visits WHERE village_code_id = ?',
      [id],
    )
    if (Number(visitRow?.c ?? 0) > 0) {
      toast.error('Unable to delete village code. There are visits associated with this village code.')
      return
    }
    await db.execute('DELETE FROM village_codes WHERE id = ?', [id])
    await refreshPendingCrudCount()
    toast.success('Village code deleted!')
    close()
  }
  catch (err) {
    console.error(err)
    toast.error('Unable to delete village code. Check that there are no visits with this village code before deleting.')
  }
  finally {
    isDeleting.value = false
  }
}
</script>

<template>
  <div>
    <PageHeader
      title="Village Codes"
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings', to: paths.settings() }, { label: 'Village Codes' }]"
    >
      <template #actions>
        <div class="flex items-center gap-2">
          <Checkbox id="show-hidden" v-model="showHidden" />
          <Label for="show-hidden">Show Hidden</Label>
        </div>
        <Button @click="openEdit(null)">
          <PhPlus aria-hidden="true" /> New Village Code
        </Button>
      </template>
    </PageHeader>

    <AppPanel title="Village codes" flush>
      <StateMessage v-if="isLoading" kind="loading" message="Loading Village Codes..." />
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Code</TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Color</TableHead>
            <TableHead>Status</TableHead>
            <TableHead class="text-right">
              Actions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="code in codes" :key="code.id">
            <TableCell data-label="Code" class="font-mono font-semibold">
              {{ code.code }}
            </TableCell>
            <TableCell data-label="Name">{{ code.name }}</TableCell>
            <TableCell data-label="Color">
              <span class="inline-flex items-center gap-2">
                <span class="size-3.5 border border-border" :style="{ backgroundColor: code.colorHex }" aria-hidden="true" />
                <span class="font-mono text-muted-foreground">{{ code.colorHex }}</span>
              </span>
            </TableCell>
            <TableCell data-label="Status">
              <Badge v-if="code.isVisible" variant="outline" class="border-success/40 bg-success-subtle text-success">
                Active
              </Badge>
              <Badge v-else variant="outline">
                Hidden
              </Badge>
            </TableCell>
            <TableCell data-stack="actions">
              <div class="flex justify-end gap-1">
                <Button size="sm" variant="outline" :aria-label="`Edit ${code.name}`" @click="openEdit(code)">
                  <PhPencilSimple aria-hidden="true" /> Edit
                </Button>
                <Button size="sm" variant="destructive" :aria-label="`Delete ${code.name}`" @click="openDelete(code)">
                  <PhTrash aria-hidden="true" /> Delete
                </Button>
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </AppPanel>

    <VillageCodeDialog v-if="mode === 'editing'" :village="active" @close="close" />
    <AppDialog v-if="mode === 'deleting' && active" title="Confirm Deletion" size="sm" @close="close">
      <p>Code: <span class="font-mono font-semibold">{{ active.code }}</span></p>
      <p>Name: {{ active.name }}</p>
      <template #footer>
        <Button variant="outline" @click="close">
          Cancel
        </Button>
        <Button variant="destructive" :disabled="isDeleting" @click="deleteVillageCode(active.id)">
          <PhTrash aria-hidden="true" /> {{ isDeleting ? 'Deleting...' : 'Confirm' }}
        </Button>
      </template>
    </AppDialog>
  </div>
</template>
