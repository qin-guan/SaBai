<script setup lang="ts">
import { PhCheck, PhCircle, PhCircleDashed } from '@phosphor-icons/vue'
import type { Journey, StationKey, StepState } from '~/utils/journey'

/**
 * A visit's journey at a glance:
 *
 *   [✓ Vitals][✓ Vision][● Consult][◌ Dental][◌ Pharmacy]
 *
 * Done steps are green, the next step to do is highlighted, other steps still
 * needed are outlined and optional steps are dashed. Each step links to that
 * station's form for this visit, so staff can move a patient along from any
 * screen. `active` marks the station currently open.
 */
const props = withDefaults(defineProps<{
  journey: Journey
  patientId: number
  visitId: number
  active?: StationKey
  /** `compact`: a single dense row for lists. */
  size?: 'compact' | 'full'
  /** Render steps as links to each station. */
  links?: boolean
}>(), { size: 'full', links: true })

const SHORT: Record<StationKey, string> = { vitals: 'Vitals', vision: 'Vision', consults: 'Consult', dental: 'Dental', pharmacy: 'Pharm' }
const steps = computed(() => journeySteps(props.journey).map(s => ({ ...s, short: SHORT[s.key] })))
const next = computed(() => nextStep(props.journey)?.key)

function stepClass(state: StepState, key: StationKey) {
  if (state === 'done')
    return 'border-success/40 bg-success-subtle text-success'
  if (key === next.value)
    return 'border-primary bg-primary text-primary-foreground'
  if (state === 'todo')
    return 'border-border bg-card text-foreground'
  return 'border-dashed border-border bg-transparent text-muted-foreground'
}

const summary = computed(() =>
  steps.value.map(s => `${s.label}: ${s.state === 'optional' ? `optional, ${s.detail.toLowerCase()}` : s.detail.toLowerCase()}`).join('; '),
)

/** Status line under each full step; a shorter one for phone-width cells. */
function status(step: { key: StationKey, state: StepState, detail: string }) {
  if (step.state === 'done')
    return { long: step.detail, short: 'Done' }
  if (step.key === next.value)
    return { long: step.detail === 'Not yet' ? 'Next' : `Next · ${step.detail}`, short: 'Next' }
  if (step.state === 'todo')
    return { long: step.detail === 'Not yet' ? 'To do' : step.detail, short: step.detail === 'Not yet' ? 'To do' : step.detail }
  return { long: step.detail, short: 'Optional' }
}
</script>

<template>
  <!-- Full: five equal cells, each a link to that station for this visit. -->
  <ol
    v-if="props.size === 'full'"
    class="@container grid min-w-0 grid-cols-5 gap-1"
    :aria-label="`Patient journey. ${summary}`"
  >
    <li v-for="step in steps" :key="step.key" class="min-w-0">
      <component
        :is="props.links ? resolveComponent('NuxtLink') : 'span'"
        :to="props.links ? patientLinkFor(step.key, props.patientId, props.visitId) : undefined"
        class="flex h-full min-w-0 flex-col justify-center gap-px border px-1.5 py-1 sm:px-2 touch:py-1.5"
        :class="[
          stepClass(step.state, step.key),
          props.links && 'cursor-pointer hover:border-primary hover:shadow-[inset_0_-2px_0_var(--color-primary)] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-ring',
          props.active === step.key && 'outline-2 outline-offset-1 outline-ring',
        ]"
        :title="props.links ? `Open ${step.label}${step.optional ? ' (optional)' : ''}: ${step.detail}` : `${step.label}: ${step.detail}`"
        :aria-current="props.active === step.key ? 'step' : undefined"
        :aria-label="`${step.label}${step.optional ? ' (optional)' : ''}: ${step.detail}${step.key === next ? ', next' : ''}`"
      >
        <span class="flex min-w-0 items-center gap-1 text-xs font-bold">
          <PhCheck v-if="step.state === 'done'" weight="bold" class="size-3.5 shrink-0" aria-hidden="true" />
          <PhCircle v-else-if="step.state === 'todo'" :weight="step.key === next ? 'fill' : 'regular'" class="size-3 shrink-0" aria-hidden="true" />
          <PhCircleDashed v-else class="size-3 shrink-0" aria-hidden="true" />
          <!-- Full names once each cell has room (~7rem), short ones before. -->
          <span class="truncate @[36rem]:hidden">{{ step.short }}</span>
          <span class="truncate @max-[36rem]:hidden">{{ step.label }}</span>
        </span>
        <span class="truncate text-xs opacity-85 @[30rem]:hidden">{{ status(step).short }}</span>
        <span class="truncate text-xs opacity-85 @max-[30rem]:hidden">{{ status(step).long }}</span>
      </component>
    </li>
  </ol>

  <!-- Compact: a single dense row for lists. -->
  <ol
    v-else
    class="flex min-w-0 items-center gap-px"
    :aria-label="`Patient journey. ${summary}`"
  >
    <li v-for="step in steps" :key="step.key" class="flex min-w-0 flex-1 items-center">
      <component
        :is="props.links ? resolveComponent('NuxtLink') : 'span'"
        :to="props.links ? patientLinkFor(step.key, props.patientId, props.visitId) : undefined"
        class="inline-flex w-full min-w-0 items-center justify-center border px-1 py-0.5 text-xs font-semibold whitespace-nowrap touch:py-1.5"
        :class="[
          stepClass(step.state, step.key),
          props.links && 'hover:border-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-ring',
          props.active === step.key && 'outline-2 outline-offset-1 outline-ring',
        ]"
        :title="`${step.label}${step.optional ? ' (optional)' : ''}: ${step.detail}`"
        :aria-current="props.active === step.key ? 'step' : undefined"
        :aria-label="`${step.label}${step.optional ? ' (optional)' : ''}: ${step.detail}${step.key === next ? ', next' : ''}`"
      >
        <span class="truncate">{{ step.short }}</span>
      </component>
    </li>
  </ol>
</template>
