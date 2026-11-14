<script setup lang="ts">
import { PhPlus, PhTag, PhX } from '@phosphor-icons/vue'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  buildTagDropdownOptions,
  hasTagSelected,
  MAX_TAG_LENGTH,
  type TagDropdownOption,
} from '~/utils/patient'

/**
 * Inline multi-tag combobox: searches existing clinic tags directly in the text
 * field and offers a "Create tag …" option in the dropdown when no exact match exists.
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
    placeholder: 'Search or create a tag…',
    describedby: undefined,
  },
)

const draft = ref('')
const isOpen = ref(false)
const activeIndex = ref(0)
const listId = useId()
const optionId = (index: number) => `${listId}-opt-${index}`
const { data: knownTags } = useTags()

const dropdownOptions = computed(() =>
  buildTagDropdownOptions(knownTags.value, model.value, draft.value, MAX_TAG_LENGTH),
)

const alreadySelectedMatch = computed(() => {
  const trimmed = draft.value.trim()
  return !!trimmed && hasTagSelected(model.value, trimmed) && dropdownOptions.value.length === 0
})

const dropdownVisible = computed(
  () => isOpen.value && (dropdownOptions.value.length > 0 || alreadySelectedMatch.value),
)

watch(draft, () => {
  activeIndex.value = 0
  if (draft.value)
    isOpen.value = true
})

watch(dropdownOptions, (options) => {
  if (activeIndex.value >= options.length)
    activeIndex.value = Math.max(0, options.length - 1)
})

watch(activeIndex, (index) => {
  if (!dropdownVisible.value)
    return
  nextTick(() => document.getElementById(optionId(index))?.scrollIntoView({ block: 'nearest' }))
})

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
  activeIndex.value = 0
}

function selectOption(option: TagDropdownOption) {
  addTag(option.tag)
}

function removeTag(tag: string) {
  model.value = model.value.filter(t => t !== tag)
}

function onKeydown(event: KeyboardEvent) {
  const count = dropdownOptions.value.length
  if (event.key === 'ArrowDown') {
    event.preventDefault()
    if (!isOpen.value) {
      isOpen.value = true
      activeIndex.value = 0
    }
    else if (count > 0) {
      activeIndex.value = (activeIndex.value + 1) % count
    }
  }
  else if (event.key === 'ArrowUp') {
    event.preventDefault()
    if (!isOpen.value) {
      isOpen.value = true
      activeIndex.value = Math.max(0, count - 1)
    }
    else if (count > 0) {
      activeIndex.value = (activeIndex.value - 1 + count) % count
    }
  }
  else if (event.key === 'Enter') {
    event.preventDefault()
    event.stopPropagation()
    const activeOption = dropdownVisible.value ? dropdownOptions.value[activeIndex.value] : undefined
    if (activeOption && !draft.value.includes(','))
      selectOption(activeOption)
    else if (draft.value.trim())
      addTag()
  }
  else if (event.key === ',') {
    event.preventDefault()
    event.stopPropagation()
    if (draft.value.trim())
      addTag()
  }
  else if (event.key === 'Escape' && isOpen.value) {
    event.preventDefault()
    event.stopPropagation()
    isOpen.value = false
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

    <div class="relative">
      <Input
        :id="props.id"
        v-model="draft"
        type="text"
        role="combobox"
        aria-autocomplete="list"
        :aria-expanded="dropdownVisible"
        :aria-controls="listId"
        :aria-activedescendant="dropdownVisible && dropdownOptions.length > 0 ? optionId(activeIndex) : undefined"
        :placeholder="props.placeholder"
        :maxlength="MAX_TAG_LENGTH"
        :aria-describedby="props.describedby"
        autocomplete="off"
        @focus="isOpen = true"
        @click="isOpen = true"
        @blur="isOpen = false"
        @keydown="onKeydown"
      />

      <ul
        v-if="dropdownVisible"
        :id="listId"
        role="listbox"
        aria-label="Tag suggestions"
        class="absolute inset-x-0 top-full z-50 mt-1 max-h-48 divide-y divide-border/40 overflow-y-auto border border-border bg-popover text-popover-foreground shadow-md"
      >
        <li
          v-for="(option, idx) in dropdownOptions"
          :id="optionId(idx)"
          :key="`${option.type}-${option.tag}`"
          role="option"
          :aria-selected="idx === activeIndex"
          class="flex w-full cursor-pointer items-center gap-2 px-2.5 py-1.5 text-left text-xs touch:py-2"
          :class="idx === activeIndex ? 'bg-accent text-accent-foreground' : 'hover:bg-accent/60'"
          @mousedown.prevent
          @mousemove="activeIndex = idx"
          @click="selectOption(option)"
        >
          <template v-if="option.type === 'existing'">
            <PhTag class="size-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
            <span class="truncate">{{ option.tag }}</span>
          </template>
          <template v-else>
            <PhPlus class="size-3.5 shrink-0 text-primary" aria-hidden="true" />
            <span class="truncate">
              Create tag <span class="font-semibold">"{{ option.tag }}"</span>
            </span>
          </template>
        </li>
        <li
          v-if="alreadySelectedMatch"
          class="px-2.5 py-2 text-xs text-muted-foreground"
        >
          Tag "{{ draft.trim() }}" is already added.
        </li>
      </ul>
    </div>
  </div>
</template>

