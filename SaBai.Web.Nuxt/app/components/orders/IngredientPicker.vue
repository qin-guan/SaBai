<script setup lang="ts">
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import type { OrderableIngredient } from '~/utils/ordering'

/**
 * Picks the active ingredient to order. Every ingredient is listed with what
 * can still be ordered; one with nothing left is listed under "Out of stock"
 * and cannot be chosen (unless it is already the selection, so the error
 * next to it can say why).
 */
defineOptions({ inheritAttrs: false })
const model = defineModel<string>({ default: '' })
const props = defineProps<{ options: readonly OrderableIngredient[] }>()

const available = computed(() => props.options.filter(o => !o.outOfStock))
const outOfStock = computed(() => props.options.filter(o => o.outOfStock))
</script>

<template>
  <NativeSelect v-model="model" v-bind="$attrs">
    <NativeSelectOption value="">
      Choose a medication
    </NativeSelectOption>
    <optgroup v-if="available.length > 0" label="Available">
      <NativeSelectOption v-for="o in available" :key="o.ingredientId" :value="o.ingredientId">
        {{ orderableLabel(o) }}
      </NativeSelectOption>
    </optgroup>
    <optgroup v-if="outOfStock.length > 0" label="Out of stock — cannot be ordered">
      <NativeSelectOption
        v-for="o in outOfStock"
        :key="o.ingredientId"
        :value="o.ingredientId"
        :disabled="o.ingredientId !== model"
      >
        {{ orderableLabel(o) }}
      </NativeSelectOption>
    </optgroup>
  </NativeSelect>
</template>
