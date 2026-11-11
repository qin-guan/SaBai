<script setup lang="ts">
import { PhKeyboard, PhMagnifyingGlass, PhUserFocus } from '@phosphor-icons/vue'
import { useResizeObserver } from '@vueuse/core'
import { Button } from '@/components/ui/button'
import { SidebarInset, SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar'

/**
 * Application shell: navy app bar (sidebar toggle, patient search, village),
 * activity sidebar (a drawer on phones) and the main work area.
 */
const header = ref<HTMLElement>()

// Publish the app bar's height so sticky sub-navigation can sit beneath it.
useResizeObserver(header, ([entry]) => {
  const height = (entry?.target as HTMLElement | undefined)?.offsetHeight ?? 0
  document.documentElement.style.setProperty('--app-header-h', `${height}px`)
})
const showShortcuts = ref(false)
const showSearch = ref(false)
const showFaceSearch = ref(false)
const route = useRoute()

/** The section you are in, named in the app bar on phones (where the search box shrinks to an icon). */
const sectionName = computed(() =>
  [...ALL_NAV_ITEMS, ...SUB_PAGES].find(item => isNavItemActive(item, route))?.name ?? "Sa'Bai",
)

function onKeydown(event: KeyboardEvent) {
  const mod = event.ctrlKey || event.metaKey

  // Ctrl/⌘+S or Ctrl/⌘+Enter: save the active form (never the browser's "Save page").
  if (mod && (event.key === 's' || event.key === 'S' || event.key === 'Enter')) {
    if (runSaveShortcut())
      event.preventDefault()
    return
  }

  // Ctrl/⌘+K: command palette (works from anywhere, including inside fields).
  if (mod && !event.altKey && (event.key === 'k' || event.key === 'K')) {
    event.preventDefault()
    showSearch.value = !showSearch.value
    return
  }

  if (event.defaultPrevented || mod || event.altKey)
    return
  if (document.querySelector('[role="dialog"], [role="menu"]'))
    return

  if (isTypingTarget(event.target)) {
    if (event.key === 'Escape' && !event.shiftKey && !event.repeat)
      (event.target as HTMLElement).blur()
    return
  }

  if (event.key === 'Escape' && !event.shiftKey && !event.repeat) {
    const back = logicalBackTarget(route)
    if (back) {
      event.preventDefault()
      navigateTo(back)
    }
    return
  }

  if (event.key === '/') {
    event.preventDefault()
    showSearch.value = true
  }
  else if ((event.key === 'f' || event.key === 'F') && route.path !== paths.scanFace()) {
    event.preventDefault()
    showFaceSearch.value = true
  }
  else if (event.key === '?') {
    event.preventDefault()
    showShortcuts.value = true
  }
}

onMounted(() => window.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))
</script>

<template>
  <SidebarProvider>
    <a
      href="#main"
      class="sr-only z-[60] bg-primary px-3 py-2 text-primary-foreground focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
    >Skip to main content</a>
    <AppSidebar />
    <SidebarInset class="min-w-0 bg-background">
      <header
        ref="header"
        class="sticky top-0 z-30 flex items-center gap-2 bg-chrome px-2 py-1.5 pt-[max(0.375rem,env(safe-area-inset-top))] text-chrome-foreground shadow-sm"
      >
        <SidebarTrigger
          class="size-8 text-chrome-foreground hover:bg-chrome-hover hover:text-white touch:size-11"
          aria-keyshortcuts="Control+B"
        />
        <div class="min-w-0 flex-1">
          <SearchTrigger class="max-sm:hidden" @open="showSearch = true" />
          <p class="truncate text-base font-semibold text-white sm:hidden" aria-hidden="true">
            {{ sectionName }}
          </p>
        </div>
        <div class="md:hidden">
          <VillageSelector tone="chrome" compact />
        </div>
        <Button
          variant="ghost"
          size="icon"
          class="text-chrome-foreground hover:bg-chrome-hover hover:text-white touch:size-11 sm:hidden"
          aria-label="Search patients and sections"
          aria-haspopup="dialog"
          @click="showSearch = true"
        >
          <PhMagnifyingGlass aria-hidden="true" />
        </Button>
        <Button
          v-if="route.path !== paths.scanFace()"
          variant="ghost"
          size="icon"
          class="text-chrome-foreground hover:bg-chrome-hover hover:text-white touch:size-11"
          aria-label="Find patient by face"
          aria-keyshortcuts="F"
          title="Find patient by face (F)"
          @click="showFaceSearch = true"
        >
          <PhUserFocus aria-hidden="true" />
        </Button>
        <div class="hidden md:block">
          <VillageSelector tone="chrome" />
        </div>
        <Button
          variant="ghost"
          size="icon"
          class="text-chrome-foreground hover:bg-chrome-hover hover:text-white touch:hidden"
          aria-label="Keyboard shortcuts"
          aria-keyshortcuts="?"
          @click="showShortcuts = true"
        >
          <PhKeyboard aria-hidden="true" />
        </Button>
      </header>
      <main id="main" tabindex="-1" class="min-w-0 flex-1 px-3 py-3 outline-none sm:px-4">
        <slot />
      </main>
    </SidebarInset>
    <UniversalSearch v-model:open="showSearch" @show-shortcuts="showShortcuts = true" />
    <FaceSearchDialog v-if="showFaceSearch" @close="showFaceSearch = false" />
    <ShortcutsDialog v-if="showShortcuts" @close="showShortcuts = false" />
  </SidebarProvider>
</template>
