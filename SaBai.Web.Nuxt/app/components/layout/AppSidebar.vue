<script setup lang="ts">
import { PhCaretRight, PhSignOut, PhUserCircle } from '@phosphor-icons/vue'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuAction,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  useSidebar,
} from '@/components/ui/sidebar'

const route = useRoute()

const GROUPS = [
  { label: 'Patients', items: NAV_ITEMS },
  // Each station is the patient list filtered to who it is waiting for.
  { label: 'Stations', items: STATION_NAV_ITEMS },
  { label: 'Pharmacy & admin', items: ADMIN_NAV_ITEMS },
]
const { isMobile, setOpenMobile, state } = useSidebar()
const { user, isAdmin } = useCurrentUser()

/** The pages nested under an entry that this user may open. */
const childrenOf = (item: NavItem) => (item.children ?? []).filter(child => !child.adminOnly || isAdmin.value)

// Nested pages are hidden while the desktop sidebar is collapsed to icons; the
// parent's icon then stands for its whole section.
const iconsOnly = computed(() => !isMobile.value && state.value === 'collapsed')
const isParentActive = (item: NavItem) =>
  iconsOnly.value || !childrenOf(item).length ? isInNavSection(item, route) : route.path === item.href

// A section opens when you are in it, and can be opened or closed by hand.
const openSections = ref(new Set<string>())
watch(() => route.path, () => {
  for (const item of ALL_NAV_ITEMS) {
    if (item.children && isInNavSection(item, route))
      openSections.value.add(item.href)
  }
}, { immediate: true })
function toggleSection(item: NavItem) {
  if (!openSections.value.delete(item.href))
    openSections.value.add(item.href)
}
const subMenuId = (item: NavItem) => `nav-sub-${item.href.replace(/\W+/g, '-')}`

// Close the mobile drawer after navigating.
watch(() => route.fullPath, () => {
  if (isMobile.value)
    setOpenMobile(false)
})

const signOut = useSignOut()
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
    </SidebarHeader>
    <SidebarContent>
      <SidebarGroup v-for="group in GROUPS" :key="group.label">
        <SidebarGroupLabel>{{ group.label }}</SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            <SidebarMenuItem v-for="item in group.items" :key="item.href">
              <SidebarMenuButton
                as-child
                :is-active="isParentActive(item)"
                :tooltip="item.name"
              >
                <NuxtLink
                  :to="item.href"
                  :aria-current="isParentActive(item) ? 'page' : undefined"
                >
                  <component :is="item.icon" aria-hidden="true" />
                  <span class="flex-1">{{ item.name }}</span>
                </NuxtLink>
              </SidebarMenuButton>
              <template v-if="childrenOf(item).length">
                <SidebarMenuAction
                  :aria-expanded="openSections.has(item.href)"
                  :aria-controls="subMenuId(item)"
                  :aria-label="`${openSections.has(item.href) ? 'Hide' : 'Show'} pages under ${item.name}`"
                  @click="toggleSection(item)"
                >
                  <PhCaretRight
                    weight="bold"
                    class="transition-transform motion-reduce:transition-none"
                    :class="openSections.has(item.href) && 'rotate-90'"
                    aria-hidden="true"
                  />
                </SidebarMenuAction>
                <SidebarMenuSub v-show="openSections.has(item.href)" :id="subMenuId(item)">
                  <SidebarMenuSubItem v-for="child in childrenOf(item)" :key="child.href">
                    <SidebarMenuSubButton as-child :is-active="isNavItemActive(child, route)">
                      <NuxtLink
                        :to="child.href"
                        :aria-current="isNavItemActive(child, route) ? 'page' : undefined"
                      >
                        <component :is="child.icon" aria-hidden="true" />
                        <span>{{ child.navLabel ?? child.name }}</span>
                      </NuxtLink>
                    </SidebarMenuSubButton>
                  </SidebarMenuSubItem>
                </SidebarMenuSub>
              </template>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroupContent>
      </SidebarGroup>
    </SidebarContent>
    <SidebarFooter class="border-t border-sidebar-border">
      <SidebarMenu>
        <SidebarMenuItem>
          <SidebarMenuButton
            as-child
            :is-active="route.path === paths.account()"
            :tooltip="user ? `My account (${user.nickname})` : 'My account'"
          >
            <NuxtLink :to="paths.account()">
              <PhUserCircle aria-hidden="true" />
              <span class="flex min-w-0 flex-1 flex-col leading-tight">
                <span class="truncate">{{ user?.nickname ?? 'My account' }}</span>
                <span v-if="user" class="truncate text-xs text-muted-foreground">{{ user.userName }}</span>
              </span>
            </NuxtLink>
          </SidebarMenuButton>
        </SidebarMenuItem>
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
