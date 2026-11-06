<script setup lang="ts">
import { PhSignOut } from '@phosphor-icons/vue'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'

const route = useRoute()

const GROUPS = [
  { label: 'Patients', items: NAV_ITEMS },
  // Each station is the patient queue filtered to who it is waiting for.
  { label: 'Stations', items: STATION_NAV_ITEMS },
  { label: 'Pharmacy & admin', items: ADMIN_NAV_ITEMS },
]
const { isMobile, setOpenMobile } = useSidebar()

// Close the mobile drawer after navigating.
watch(() => route.fullPath, () => {
  if (isMobile.value)
    setOpenMobile(false)
})

async function signOut() {
  clearToken()
  // Drafts and recent patients hold patient data; don't leave them on a shared device.
  clearAllFormDrafts()
  clearRecentPatients()
  await navigateTo(paths.login())
}
</script>

<template>
  <Sidebar collapsible="icon">
    <SidebarHeader class="border-b border-sidebar-border">
      <NuxtLink
        :to="paths.home()"
        class="flex items-center gap-2 px-1 py-1 font-bold group-data-[collapsible=icon]:justify-center"
      >
        <img src="/favicon.ico" alt="" class="size-7 shrink-0 bg-chrome p-1">
        <span class="truncate text-sm group-data-[collapsible=icon]:hidden">Project Sa'bai</span>
      </NuxtLink>
      <div class="md:hidden">
        <VillageSelector />
      </div>
    </SidebarHeader>
    <SidebarContent>
      <SidebarGroup v-for="group in GROUPS" :key="group.label">
        <SidebarGroupLabel>{{ group.label }}</SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            <SidebarMenuItem v-for="item in group.items" :key="item.href">
              <SidebarMenuButton
                as-child
                :is-active="isNavItemActive(item, route)"
                :tooltip="item.name"
              >
                <NuxtLink
                  :to="item.href"
                  :aria-current="isNavItemActive(item, route) ? 'page' : undefined"
                >
                  <component :is="item.icon" aria-hidden="true" />
                  <span class="flex-1">{{ item.name }}</span>
                </NuxtLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroupContent>
      </SidebarGroup>
    </SidebarContent>
    <SidebarFooter class="border-t border-sidebar-border">
      <SidebarMenu>
        <SidebarMenuItem>
          <SidebarMenuButton tooltip="Sign out" @click="signOut">
            <PhSignOut aria-hidden="true" />
            <span>Sign out</span>
          </SidebarMenuButton>
        </SidebarMenuItem>
      </SidebarMenu>
    </SidebarFooter>
  </Sidebar>
</template>
