<script setup lang="ts">
import { PhCheck, PhCircle, PhCircleDashed, PhArrowRight } from '@phosphor-icons/vue'
import type { Journey, StationKey, StepState } from '~/utils/journey'

/**
 * A visit's journey at a glance:
 *
 *   ✓ Vitals → ✓ Vision → ▸ Consult → ◌ Dental → ◌ Pharmacy
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
</script>

<template>
  <ol
    class="flex min-w-0 items-center"
    :class="props.size === 'compact' ? 'gap-px' : 'flex-wrap gap-1'"
    :aria-label="`Patient journey. ${summary}`"
  >
    <li
      v-for="(step, index) in steps"
      :key="step.key"
      class="flex items-center"
      :class="props.size === 'compact' ? 'min-w-0 flex-1' : 'gap-1'"
    >
      <PhArrowRight
        v-if="index > 0 && props.size === 'full'"
        class="size-3 shrink-0 text-muted-foreground/60"
        aria-hidden="true"
      />
      <component
        :is="props.links ? resolveComponent('NuxtLink') : 'span'"
        :to="props.links ? patientLinkFor(step.key, props.patientId, props.visitId) : undefined"
        class="inline-flex min-w-0 items-center gap-1 border font-semibold whitespace-nowrap"
        :class="[
          stepClass(step.state, step.key),
          props.size === 'compact'
            ? 'w-full justify-center px-1 py-0.5 text-[0.6875rem] touch:py-1.5 touch:text-xs'
            : 'px-2 py-1 text-xs touch:px-3 touch:py-2 touch:text-sm',
          props.links && 'hover:border-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-ring',
          props.active === step.key && 'outline-2 outline-offset-1 outline-ring',
        ]"
        :title="`${step.label}${step.optional ? ' (optional)' : ''}: ${step.detail}`"
        :aria-current="props.active === step.key ? 'step' : undefined"
        :aria-label="`${step.label}${step.optional ? ' (optional)' : ''}: ${step.detail}${step.key === next ? ', next' : ''}`"
      >
        <!-- In the compact bar the green fill alone marks a done step. -->
        <PhCheck v-if="step.state === 'done' && props.size === 'full'" weight="bold" class="size-3 shrink-0" aria-hidden="true" />
        <template v-else-if="props.size === 'full'">
          <PhCircle v-if="step.state === 'todo'" :weight="step.key === next ? 'fill' : 'regular'" class="size-3 shrink-0" aria-hidden="true" />
          <PhCircleDashed v-else class="size-3 shrink-0" aria-hidden="true" />
        </template>
        <span class="truncate">{{ props.size === 'compact' ? step.short : step.label }}</span>
        <span
          v-if="props.size === 'full' && (step.state !== 'done' || step.key === 'pharmacy') && step.detail !== 'Not yet'"
          class="font-normal opacity-80"
        >· {{ step.detail }}</span>
      </component>
    </li>
  </ol>
</template>
