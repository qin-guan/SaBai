<script setup lang="ts">
import { PhPlus, PhX } from '@phosphor-icons/vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * Freeform multi-tag editor with case-insensitive deduplication and autocomplete
 * from existing clinic tags.
 */
const model = defineModel<string[]>({ default: () => [] })
const props = withDefaults(
  defineProps<{
    id?: string
    placeholder?: string
    describedby?: string
  }>(),
  {
    id: undefined,
    placeholder: 'Add a tag and press Enter…',
    describedby: undefined,
  },
)

const MAX_TAG_LENGTH = 64
const draft = ref('')
const listId = useId()
const { data: knownTags } = useTags()

const availableSuggestions = computed(() => {
  const current = new Set(model.value.map(t => t.toLowerCase()))
  return (knownTags.value ?? []).filter(t => !current.has(t.toLowerCase()))
})

const browseFilter = ref('')
const showBrowse = ref(false)
const browseMatches = computed(() => {
  const q = browseFilter.value.trim().toLowerCase()
  return availableSuggestions.value.filter(t => !q || t.toLowerCase().includes(q))
})

function pickTag(tag: string) {
  addTag(tag)
}

function addTag(raw?: string) {
  const parts = (raw ?? draft.value)
    .split(',')
    .map(x => x.trim())
    .filter(Boolean)
  if (parts.length === 0) {
    draft.value = ''
    return
  }

  const next = [...model.value]
  for (const part of parts) {
    const clipped = part.slice(0, MAX_TAG_LENGTH)
    if (!next.some(existing => existing.toLowerCase() === clipped.toLowerCase()))
      next.push(clipped)
  }
  model.value = next
  draft.value = ''
}

function removeTag(tag: string) {
  model.value = model.value.filter(t => t !== tag)
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' || event.key === ',') {
    event.preventDefault()
    event.stopPropagation()
    addTag()
  }
  else if (event.key === 'Backspace' && !draft.value && model.value.length > 0) {
    model.value = model.value.slice(0, -1)
  }
}
</script>

<template>
  <div class="flex flex-col gap-2">
    <div v-if="model.length > 0" class="flex flex-wrap gap-1.5">
      <Badge
        v-for="tag in model"
        :key="tag"
        variant="secondary"
        class="gap-1 pr-1"
      >
        <span>{{ tag }}</span>
        <button
          type="button"
          class="rounded-sm p-0.5 text-muted-foreground hover:bg-muted-foreground/15 hover:text-foreground focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          :aria-label="`Remove tag ${tag}`"
          @click="removeTag(tag)"
        >
          <PhX class="size-3" aria-hidden="true" />
        </button>
      </Badge>
    </div>
    <div class="flex gap-2">
      <Input
        :id="props.id"
        v-model="draft"
        type="text"
        :list="availableSuggestions.length > 0 ? listId : undefined"
        :placeholder="props.placeholder"
        :maxlength="MAX_TAG_LENGTH"
        :aria-describedby="props.describedby"
        autocomplete="off"
        @keydown="onKeydown"
        @blur="addTag()"
      />
      <Button
        type="button"
        variant="outline"
        size="sm"
        class="shrink-0"
        :disabled="!draft.trim()"
        @click="addTag()"
      >
        <PhPlus aria-hidden="true" /> Add
      </Button>
    </div>
    <div v-if="availableSuggestions.length > 0" class="flex flex-col gap-1.5">
      <button
        type="button"
        class="self-start text-xs text-primary hover:underline focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        :aria-expanded="showBrowse"
        @click="showBrowse = !showBrowse"
      >
        {{ showBrowse ? 'Hide existing tags' : `Browse existing tags (${availableSuggestions.length})` }}
      </button>
      <div v-if="showBrowse" class="flex flex-col gap-1.5 border border-border p-2">
        <Input
          v-model="browseFilter"
          type="search"
          class="h-7 text-xs"
          placeholder="Filter existing tags…"
          aria-label="Filter existing tags"
          autocomplete="off"
          @keydown.enter.prevent.stop
        />
        <ul class="flex max-h-32 flex-wrap gap-1.5 overflow-y-auto" aria-label="Existing tags">
          <li v-for="tag in browseMatches" :key="tag">
            <button
              type="button"
              class="inline-flex"
              :aria-label="`Add tag ${tag}`"
              @click="pickTag(tag)"
            >
              <Badge variant="outline" class="cursor-pointer gap-1 hover:bg-accent">
                <PhPlus class="size-3" aria-hidden="true" />{{ tag }}
              </Badge>
            </button>
          </li>
          <li v-if="browseMatches.length === 0" class="text-xs text-muted-foreground">
            No matching tags.
          </li>
        </ul>
      </div>
    </div>
    <datalist v-if="availableSuggestions.length > 0" :id="listId">
      <option v-for="tag in availableSuggestions" :key="tag" :value="tag" />
    </datalist>
  </div>
</template>
