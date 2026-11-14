<script setup lang="ts">
import { Badge } from '@/components/ui/badge'

/**
 * Clickable list of all existing clinic tags for filtering a list.
 * v-model is the selected tag (case-insensitive match) or '' for none.
 */
const model = defineModel<string>({ default: '' })
const { data: tags } = useTags()

function toggle(tag: string) {
  model.value = model.value.toLowerCase() === tag.toLowerCase() ? '' : tag
}
</script>

<template>
  <div v-if="(tags ?? []).length > 0" class="flex flex-wrap items-center gap-1.5" role="group" aria-label="Filter by tag">
    <span class="text-xs text-muted-foreground">Tags:</span>
    <button
      v-for="tag in tags"
      :key="tag"
      type="button"
      class="inline-flex focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
      :aria-pressed="model.toLowerCase() === tag.toLowerCase()"
      @click="toggle(tag)"
    >
      <Badge :variant="model.toLowerCase() === tag.toLowerCase() ? 'default' : 'outline'" class="cursor-pointer hover:bg-accent">
        {{ tag }}
      </Badge>
    </button>
  </div>
</template>
