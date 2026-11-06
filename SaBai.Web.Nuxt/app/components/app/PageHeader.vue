<script setup lang="ts">
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'

export interface Crumb {
  label: string
  to?: string
}

const props = defineProps<{
  title: string
  description?: string
  breadcrumbs?: Crumb[]
}>()
</script>

<template>
  <div class="mb-3 flex flex-wrap items-end justify-between gap-x-4 gap-y-2">
    <div class="min-w-0">
      <Breadcrumb v-if="props.breadcrumbs?.length" class="mb-1">
        <BreadcrumbList>
          <template v-for="(item, index) in props.breadcrumbs" :key="index">
            <BreadcrumbItem>
              <BreadcrumbLink v-if="item.to" as-child>
                <NuxtLink :to="item.to">
                  {{ item.label }}
                </NuxtLink>
              </BreadcrumbLink>
              <BreadcrumbPage v-else>
                {{ item.label }}
              </BreadcrumbPage>
            </BreadcrumbItem>
            <BreadcrumbSeparator v-if="index < props.breadcrumbs.length - 1" />
          </template>
        </BreadcrumbList>
      </Breadcrumb>
      <h1 class="text-base font-bold leading-tight sm:text-lg">
        {{ props.title }}
      </h1>
      <p v-if="props.description" class="text-xs text-muted-foreground">
        {{ props.description }}
      </p>
    </div>
    <div v-if="$slots.actions" class="flex flex-wrap items-center gap-2">
      <slot name="actions" />
    </div>
  </div>
</template>
