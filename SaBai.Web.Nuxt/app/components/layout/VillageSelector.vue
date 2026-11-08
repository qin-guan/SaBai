<script setup lang="ts">
import { PhCaretDown, PhGear, PhMapPin } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

/** Picks the village that new patients and visits are recorded against. */
const props = defineProps<{
  tone?: 'chrome' | 'default'
  /** Narrow screens: just the colour swatch and village code. */
  compact?: boolean
}>()
const { selectedVillageCodeId, selectedVillage, setSelectedVillageCodeId, villageCodes, isLoading }
  = useVillageCode()

const selected = computed({
  get: () => (selectedVillageCodeId.value === null ? '' : String(selectedVillageCodeId.value)),
  set: (v: string) => setSelectedVillageCodeId(v ? Number(v) : null),
})
</script>

<template>
  <div v-if="isLoading" class="px-2 py-1 text-xs opacity-80 touch:text-sm" role="status">
    Loading villages…
  </div>
  <NuxtLink
    v-else-if="!villageCodes || villageCodes.length === 0"
    :to="paths.settingsVillageCodes()"
    class="border border-warning-border bg-warning-subtle px-2 py-1 text-xs text-warning hover:underline touch:text-sm"
    title="Manage village codes"
  >
    No villages available
  </NuxtLink>
  <DropdownMenu v-else>
    <DropdownMenuTrigger as-child>
      <Button
        variant="outline"
        class="justify-start gap-2"
        :class="[
          props.compact ? 'w-auto px-2' : 'w-full md:w-48 xl:w-56',
          props.tone === 'chrome' && 'border-chrome-hover bg-chrome-hover text-chrome-foreground hover:bg-chrome-hover/80 hover:text-white',
        ]"
        aria-label="Village for new patients and visits"
      >
        <PhMapPin v-if="!props.compact || !selectedVillage" aria-hidden="true" />
        <template v-if="selectedVillage">
          <span class="size-2.5 shrink-0" :style="{ backgroundColor: selectedVillage.colorHex }" aria-hidden="true" />
          <span class="max-w-24 truncate" :class="props.compact && 'font-mono font-semibold'">{{ props.compact ? selectedVillage.code : selectedVillage.name }}</span>
        </template>
        <span v-else class="font-bold" :class="props.tone === 'chrome' ? 'text-amber-200' : 'text-warning'">{{ props.compact ? 'Village' : 'Select village' }}</span>
        <PhCaretDown class="ml-auto" aria-hidden="true" />
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end" class="w-64">
      <DropdownMenuLabel>Village for new patients &amp; visits</DropdownMenuLabel>
      <DropdownMenuSeparator />
      <DropdownMenuRadioGroup v-model="selected">
        <DropdownMenuRadioItem v-for="village in villageCodes" :key="village.id" :value="String(village.id)">
          <span class="size-2.5 shrink-0" :style="{ backgroundColor: village.colorHex }" aria-hidden="true" />
          <span class="flex-1">{{ village.name }}</span>
          <span class="text-muted-foreground">{{ village.code }}</span>
        </DropdownMenuRadioItem>
      </DropdownMenuRadioGroup>
      <DropdownMenuSeparator />
      <DropdownMenuItem as-child>
        <NuxtLink :to="paths.settingsVillageCodes()" class="flex cursor-pointer items-center gap-2 text-xs text-muted-foreground">
          <PhGear class="size-3.5" aria-hidden="true" />
          <span>Manage village codes…</span>
        </NuxtLink>
      </DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
