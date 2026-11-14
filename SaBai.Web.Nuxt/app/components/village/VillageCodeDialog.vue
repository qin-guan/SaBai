<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import type { LocalVillageCode } from '~/db/types'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

/**
 * Create or edit a village code. A new-code draft is kept in localStorage
 * (same key as SaBai.Web) so an interrupted entry is not lost.
 */
const props = defineProps<{ village: LocalVillageCode | null }>()
const emit = defineEmits<{ close: [] }>()

const DRAFT_KEY = 'village-codes-form'
const DEFAULT_FORM = { code: '', name: '', colorHex: '#3b82f6', isVisible: true }
type FormFields = typeof DEFAULT_FORM

const isEditing = computed(() => !!props.village)

function readDraft(): FormFields {
  try {
    const stored = window.localStorage.getItem(DRAFT_KEY)
    return stored ? { ...DEFAULT_FORM, ...JSON.parse(stored) } : { ...DEFAULT_FORM }
  }
  catch {
    return { ...DEFAULT_FORM }
  }
}

const initial: FormFields = props.village
  ? { code: props.village.code, name: props.village.name, colorHex: props.village.colorHex, isVisible: props.village.isVisible }
  : readDraft()
const form = ref<FormFields>({ ...initial })
const errors = ref<Partial<Record<keyof FormFields, string>>>({})
const formEl = ref<HTMLFormElement>()
const isDirty = computed(() => JSON.stringify(form.value) !== JSON.stringify(initial))

// Only persist drafts for new codes.
let draftTimer: number | undefined
watch(form, (value) => {
  if (isEditing.value)
    return
  window.clearTimeout(draftTimer)
  draftTimer = window.setTimeout(() => {
    try {
      window.localStorage.setItem(DRAFT_KEY, JSON.stringify(value))
    }
    catch {}
  }, 1000)
}, { deep: true })
onBeforeUnmount(() => window.clearTimeout(draftTimer))

const saving = ref(false)

async function submit() {
  if (saving.value)
    return
  errors.value = {}
  if (!form.value.code.trim())
    errors.value.code = 'Missing Code!'
  if (!form.value.name.trim())
    errors.value.name = 'Missing Name!'
  if (Object.keys(errors.value).length) {
    focusFirstInvalid(formEl.value)
    return
  }
  if (isEditing.value && !isDirty.value) {
    toast.error('Fields not changed!')
    return
  }

  saving.value = true
  try {
    const db = getPowerSyncInstance()
    const code = form.value.code.trim()
    const name = form.value.name.trim()
    const colorHex = form.value.colorHex
    const isVisible = form.value.isVisible ? 1 : 0

    if (isEditing.value) {
      await db.execute(
        `UPDATE village_codes
         SET code = ?, name = ?, color_hex = ?, is_visible = ?, version = COALESCE(version, 0) + 1
         WHERE id = ?`,
        [code, name, colorHex, isVisible, props.village!.id],
      )
      await refreshPendingCrudCount()
      emit('close')
      toast.success('Village code updated!')
    }
    else {
      await db.execute(
        `INSERT INTO village_codes (id, version, code, name, color_hex, is_visible, created_at)
         VALUES (?, ?, ?, ?, ?, ?, ?)`,
        [uuidv7(), 1, code, name, colorHex, isVisible, new Date().toISOString()],
      )
      await refreshPendingCrudCount()
      window.clearTimeout(draftTimer)
      try {
        window.localStorage.removeItem(DRAFT_KEY)
      }
      catch {}
      emit('close')
      toast.success('Village code created!')
    }
  }
  catch (err) {
    console.error(err)
    toast.error(isEditing.value ? 'Unable to update village code.' : 'Unable to create village code.')
  }
  finally {
    saving.value = false
  }
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog :title="isEditing ? 'Edit Village Code' : 'New Village Code'" size="sm" @close="emit('close')">
    <form id="village-code-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <FormField v-slot="{ id, describedby, invalid }" label="Code" required :error="errors.code">
        <Input :id="id" v-model="form.code" placeholder="e.g. V001" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Name" required :error="errors.name">
        <Input :id="id" v-model="form.name" placeholder="Central Village" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField v-slot="{ id }" label="Color">
        <div class="flex gap-2">
          <input :id="id" v-model="form.colorHex" type="color" class="h-8 w-14 shrink-0 cursor-pointer border border-input bg-card p-0.5 touch:h-11">
          <Input :model-value="form.colorHex" readonly aria-label="Color value" class="bg-muted font-mono" />
        </div>
      </FormField>
      <div class="flex items-center gap-2">
        <Checkbox id="village-visible" v-model="form.isVisible" />
        <Label for="village-visible">Is Visible?</Label>
      </div>
    </form>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="village-code-form" variant="success" :disabled="saving">
        <PhFloppyDisk aria-hidden="true" /> Save
      </Button>
    </template>
  </AppDialog>
</template>
