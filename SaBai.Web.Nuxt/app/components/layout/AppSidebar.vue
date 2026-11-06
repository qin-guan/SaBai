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
const { isMobile, setOpenMobile } = useSidebar()

// Close the mobile drawer after navigating.
watch(() => route.fullPath, () => {
  if (isMobile.value)
    setOpenMobile(false)
})

async function signOut() {
  clearToken()
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
      <SidebarGroup>
        <SidebarGroupLabel>Activities</SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            <SidebarMenuItem v-for="item in NAV_ITEMS" :key="item.href">
              <SidebarMenuButton
                as-child
                :is-active="isNavItemActive(item, route.path)"
                :tooltip="`${item.name} (Alt+${item.key})`"
              >
                <NuxtLink
                  :to="item.href"
                  :aria-current="isNavItemActive(item, route.path) ? 'page' : undefined"
                  :aria-keyshortcuts="`Alt+${item.key}`"
                >
                  <component :is="item.icon" aria-hidden="true" />
                  <span class="flex-1">{{ item.name }}</span>
                  <span
                    class="hidden text-[0.625rem] text-muted-foreground [@media(pointer:fine)]:inline group-data-[collapsible=icon]:hidden"
                    aria-hidden="true"
                  >Alt+{{ item.key }}</span>
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
