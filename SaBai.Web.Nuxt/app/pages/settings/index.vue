<script setup lang="ts">
import { PhCaretRight } from '@phosphor-icons/vue'

useHead({ title: "Settings · Sa'Bai" })

const { isAdmin } = useCurrentUser()
const settingsItems = computed(() =>
  [...SETTINGS_PAGES, MY_ACCOUNT_PAGE].filter(item => !item.adminOnly || isAdmin.value),
)
</script>

<template>
  <div>
    <PageHeader
      title="Settings"
      description="Manage configuration for this clinic deployment."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings' }]"
    />
    <AppPanel title="Configuration" flush>
      <ul class="divide-y divide-border">
        <li v-for="item in settingsItems" :key="item.name">
          <NuxtLink :to="item.href" class="flex items-center gap-3 px-3 py-3 hover:bg-accent focus-visible:bg-accent">
            <component :is="item.icon" class="size-5 text-chrome" aria-hidden="true" />
            <div class="flex-1">
              <p class="font-bold">
                {{ item.name }}
              </p>
              <p class="text-xs text-muted-foreground touch:text-sm">
                {{ item.description }}
              </p>
            </div>
            <PhCaretRight class="size-4 text-muted-foreground" aria-hidden="true" />
          </NuxtLink>
        </li>
      </ul>
    </AppPanel>
  </div>
</template>
