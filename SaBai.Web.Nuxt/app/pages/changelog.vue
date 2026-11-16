<script setup lang="ts">
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { CHANGELOG_CATEGORY_META, CHANGELOG_DAYS, type ChangelogCategory } from '@/utils/changelog'

useHead({ title: "Changelog · Sa'Bai" })

const selectedCategory = ref<'all' | ChangelogCategory>('all')

const categories: Array<{ key: 'all' | ChangelogCategory, label: string, count: number }> = [
  {
    key: 'all',
    label: 'All',
    count: CHANGELOG_DAYS.reduce((sum, day) => sum + day.entries.length, 0),
  },
  ...(['feature', 'improvement', 'fix', 'security', 'platform'] as const).map(key => ({
    key,
    label: CHANGELOG_CATEGORY_META[key].label,
    count: CHANGELOG_DAYS.reduce(
      (sum, day) => sum + day.entries.filter(entry => entry.category === key).length,
      0,
    ),
  })),
]

const filteredDays = computed(() => {
  if (selectedCategory.value === 'all')
    return CHANGELOG_DAYS
  return CHANGELOG_DAYS
    .map(day => ({
      ...day,
      entries: day.entries.filter(entry => entry.category === selectedCategory.value),
    }))
    .filter(day => day.entries.length > 0)
})
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      title="Changelog"
      description="Updates and improvements to Project Sa'bai from the past 7 days (Nov 10 – Nov 16, 2026)."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Changelog' }]"
    />

    <div class="flex flex-wrap items-center gap-1.5" role="group" aria-label="Filter updates by category">
      <Button
        v-for="cat in categories"
        :key="cat.key"
        size="xs"
        :variant="selectedCategory === cat.key ? 'default' : 'outline'"
        @click="selectedCategory = cat.key"
      >
        {{ cat.label }}
        <span class="ml-1 opacity-75">({{ cat.count }})</span>
      </Button>
    </div>

    <AppPanel
      v-for="day in filteredDays"
      :key="day.date"
      :title="day.label"
      :description="day.summary"
      flush
    >
      <ul class="divide-y divide-border">
        <li
          v-for="entry in day.entries"
          :key="entry.title"
          class="flex flex-col gap-1 px-3 py-2.5"
        >
          <div class="flex flex-wrap items-center gap-2">
            <Badge
              :variant="CHANGELOG_CATEGORY_META[entry.category].variant"
              :class="CHANGELOG_CATEGORY_META[entry.category].badgeClass"
            >
              {{ CHANGELOG_CATEGORY_META[entry.category].label }}
            </Badge>
            <h3 class="text-sm font-semibold text-foreground">
              {{ entry.title }}
            </h3>
            <span
              v-if="entry.prs?.length"
              class="ml-auto font-mono text-xs text-muted-foreground"
            >
              {{ entry.prs.map(pr => `#${pr}`).join(', ') }}
            </span>
          </div>
          <p class="text-xs leading-relaxed text-muted-foreground touch:text-sm">
            {{ entry.description }}
          </p>
        </li>
      </ul>
    </AppPanel>
  </div>
</template>
