<script setup lang="ts">
import { PhPencilSimple, PhPlus, PhTrash } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { ReferralPartner } from '@/composables/useReferralPartners'

useHead({ title: "Referral Partners · Sa'Bai" })

const { data: partners, isLoading, isError } = useReferralPartnersList()
const deletePartner = useDeleteReferralPartner()

const mode = ref<'editing' | 'deleting' | null>(null)
const active = ref<ReferralPartner | null>(null)

function openEdit(partner: ReferralPartner | null) {
  active.value = partner
  mode.value = 'editing'
}

function openDelete(partner: ReferralPartner) {
  active.value = partner
  mode.value = 'deleting'
}

function close() {
  mode.value = null
  active.value = null
}

function confirmDelete() {
  if (!active.value || deletePartner.isPending.value) return
  deletePartner.mutate(active.value.id, {
    onSuccess: () => {
      toast.success('Referral partner deleted')
      close()
    },
    onError: (err) => {
      toast.error(
        getApiErrorMessage(
          err,
          'Unable to delete referral partner. If referrals already reference this partner, deactivate it instead.',
        ),
      )
    },
  })
}
</script>

<template>
  <div>
    <PageHeader
      title="Referral Partners"
      description="Manage partner clinics and hospitals for patient referrals."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings', to: paths.settings() }, { label: 'Referral Partners' }]"
    >
      <template #actions>
        <Button @click="openEdit(null)">
          <PhPlus aria-hidden="true" /> New Referral Partner
        </Button>
      </template>
    </PageHeader>

    <AppPanel title="Referral partners" flush>
      <StateMessage v-if="isLoading" kind="loading" message="Loading referral partners..." />
      <StateMessage v-else-if="isError" kind="error" message="Failed to load referral partners." />
      <div v-else-if="!partners || partners.length === 0" class="p-8 text-center text-sm text-muted-foreground">
        No referral partners yet. Add a clinic or hospital to get started.
      </div>
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Remarks</TableHead>
            <TableHead>Status</TableHead>
            <TableHead class="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="partner in partners" :key="partner.id">
            <TableCell data-label="Name" class="font-semibold">
              {{ partner.name }}
            </TableCell>
            <TableCell data-label="Remarks" class="text-muted-foreground">
              {{ partner.remarks || '—' }}
            </TableCell>
            <TableCell data-label="Status">
              <Badge v-if="partner.isActive" variant="outline" class="border-success/40 bg-success-subtle text-success">
                Active
              </Badge>
              <Badge v-else variant="outline">
                Inactive
              </Badge>
            </TableCell>
            <TableCell data-label="Actions" class="text-right">
              <div class="inline-flex items-center gap-1.5">
                <Button variant="outline" size="sm" @click="openEdit(partner)">
                  <PhPencilSimple class="size-3.5" aria-hidden="true" />
                  <span>Edit</span>
                </Button>
                <Button variant="outline" size="sm" class="text-destructive hover:text-destructive" @click="openDelete(partner)">
                  <PhTrash class="size-3.5" aria-hidden="true" />
                  <span>Delete</span>
                </Button>
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </AppPanel>

    <ReferralPartnerDialog v-if="mode === 'editing'" :partner="active" @close="close" />

    <AppDialog
      v-if="mode === 'deleting' && active"
      title="Delete Referral Partner"
      size="sm"
      @close="close"
    >
      <p class="text-sm text-foreground">
        Are you sure you want to delete <strong>{{ active.name }}</strong>?
      </p>
      <p class="mt-2 text-xs text-muted-foreground">
        Partners that are already referenced by referrals cannot be deleted — deactivate them instead.
      </p>
      <template #footer>
        <Button variant="outline" @click="close">Cancel</Button>
        <Button variant="destructive" :disabled="deletePartner.isPending.value" @click="confirmDelete">
          {{ deletePartner.isPending.value ? 'Deleting…' : 'Delete' }}
        </Button>
      </template>
    </AppDialog>
  </div>
</template>
