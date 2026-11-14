<script setup lang="ts">
import { PhCheck, PhX } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsOrdersUpdatePatchEndpoint } from '@sabai/sdk/clients'
import type { SaBaiAPIDomainOrderStatusKey } from '@sabai/sdk/types'
import { isNetworkError } from '~/offline/network'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Orders · Sa'Bai" })

/**
 * Pending pharmacy orders by patient and visit. The list is read from the
 * cache when offline. Approving takes stock and cancelling frees the order,
 * so both are checked by the server and need a connection: they are not
 * queued.
 */
const { data: patients, isPending, isError, error } = usePharmacyOrders()
const { isOffline } = useSyncState()
const queryClient = useQueryClient()

const NEEDS_CONNECTION = 'Approving or cancelling an order needs a connection to the server. Try again when you are back online.'

const decide = useMutation({
  mutationFn: (vars: { orderId: string, status: SaBaiAPIDomainOrderStatusKey }) =>
    saBaiAPIEndpointsOrdersUpdatePatchEndpoint({
      ...apiConfig,
      path: { id: vars.orderId },
      body: { status: vars.status },
    }).unwrap(),
  onSuccess: (_result, vars) => {
    toast.success(vars.status === 'APPROVED' ? 'Order approved.' : 'Order cancelled.')
  },
  onError: (err) => {
    console.error(err)
    toast.error(isNetworkError(err) ? NEEDS_CONNECTION : getApiErrorMessage(err, 'Unable to update the order.'))
  },
  // Stock levels and the pending list both change, so refetch them either way.
  onSettled: () => Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.pharmacyOrders() }),
    queryClient.invalidateQueries({ queryKey: queryKeys.medicationStock() }),
  ]),
})

function respond(orderId: string, status: SaBaiAPIDomainOrderStatusKey) {
  if (decide.isPending.value)
    return
  if (isOffline.value) {
    toast.error(NEEDS_CONNECTION)
    return
  }
  decide.mutate({ orderId, status })
}
</script>

<template>
  <div>
    <PageHeader title="Orders" description="Approve or cancel pending medication orders." :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Orders' }]" />

    <StateMessage
      v-if="isOffline"
      kind="warning"
      class="mb-3"
      :message="patients ? 'You are offline. This list may be out of date, and orders can only be approved or cancelled when connected.' : NEEDS_CONNECTION"
    />

    <StateMessage v-if="!patients && isError" kind="error" :message="describeLoadError(error, 'Unable to load orders.')" />
    <StateMessage v-else-if="!patients && isPending" kind="loading" message="Loading orders..." />
    <StateMessage v-else-if="!patients || patients.length === 0" message="No pending orders." />

    <div v-else class="flex flex-col gap-3">
      <template v-for="patient in patients" :key="patient.patient.displayId">
        <AppPanel
          v-for="visit in patient.visits"
          :key="visit.visitId"
          :title="`${patient.patient.displayId} · ${patient.patient.name}`"
          :description="`Visit ${new Date(visit.visitDate).toLocaleDateString()}`"
          flush
        >
          <ul v-if="visit.diagnoses.length" class="border-b border-border px-3 py-2 text-xs touch:text-sm">
            <li v-for="(diagnosis, index) in visit.diagnoses" :key="index">
              <span v-if="diagnosis.category" class="font-semibold">{{ diagnosis.category }}: </span>{{ diagnosis.details }}
            </li>
          </ul>
          <Table class="grid-table stack-table">
            <TableHeader>
              <TableRow>
                <TableHead>Medication</TableHead>
                <TableHead class="text-right">
                  Quantity
                </TableHead>
                <TableHead>Dosage</TableHead>
                <TableHead class="text-right">
                  Actions
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow v-for="order in visit.orders" :key="order.orderId">
                <TableCell data-stack="title" class="font-semibold">
                  {{ order.medicationName }}
                  <Badge v-if="order.isLowStock" variant="destructive" class="ml-1">
                    Low stock
                  </Badge>
                </TableCell>
                <TableCell data-label="Quantity" class="text-right tabular-nums">
                  {{ order.quantity }}
                </TableCell>
                <TableCell data-label="Dosage">
                  {{ order.dosageInstructions }}
                </TableCell>
                <TableCell data-stack="actions">
                  <div class="flex justify-end gap-1">
                    <Button
                      size="sm"
                      variant="success"
                      :disabled="decide.isPending.value || isOffline"
                      :aria-label="`Approve ${order.medicationName} for ${patient.patient.name}`"
                      @click="respond(order.orderId, 'APPROVED')"
                    >
                      <PhCheck aria-hidden="true" /> Approve
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      :disabled="decide.isPending.value || isOffline"
                      :aria-label="`Cancel ${order.medicationName} for ${patient.patient.name}`"
                      @click="respond(order.orderId, 'CANCELLED')"
                    >
                      <PhX aria-hidden="true" /> Cancel
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </AppPanel>
      </template>
    </div>
  </div>
</template>
