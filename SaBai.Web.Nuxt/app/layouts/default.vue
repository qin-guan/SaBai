<script setup lang="ts">
import { PhKeyboard } from '@phosphor-icons/vue'
import { useResizeObserver } from '@vueuse/core'
import { Button } from '@/components/ui/button'
import { SidebarInset, SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar'

/**
 * Application shell: navy app bar (sidebar toggle, patient search, village),
 * activity sidebar (a drawer on phones) and the main work area.
 */
const search = ref<{ focus: () => void }>()
const header = ref<HTMLElement>()

// Publish the app bar's height so sticky sub-navigation can sit beneath it.
useResizeObserver(header, ([entry]) => {
  const height = (entry?.target as HTMLElement | undefined)?.offsetHeight ?? 0
  document.documentElement.style.setProperty('--app-header-h', `${height}px`)
})
const showShortcuts = ref(false)

function onKeydown(event: KeyboardEvent) {
  const mod = event.ctrlKey || event.metaKey

  // Ctrl/⌘+S or Ctrl/⌘+Enter: save the active form (never the browser's "Save page").
  if (mod && (event.key === 's' || event.key === 'S' || event.key === 'Enter')) {
    if (runSaveShortcut())
      event.preventDefault()
    return
  }

  // Alt+1…9: switch activity. Uses `code` so it works with any layout / on macOS.
  if (event.altKey && !mod && /^Digit[1-9]$/.test(event.code)) {
    const item = NAV_ITEMS.find(i => i.key === event.code.slice(5))
    if (item) {
      event.preventDefault()
      navigateTo(item.href)
    }
    return
  }

  if (mod || event.altKey || isTypingTarget(event.target))
    return
  if (document.querySelector('[role="dialog"]'))
    return

  if (event.key === '/') {
    event.preventDefault()
    search.value?.focus()
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
          class="size-8 text-chrome-foreground hover:bg-chrome-hover hover:text-white [@media(pointer:coarse)]:size-11"
          aria-keyshortcuts="Control+B"
        />
        <div class="min-w-0 flex-1">
          <PatientSearch ref="search" />
        </div>
        <div class="hidden md:block">
          <VillageSelector tone="chrome" />
        </div>
        <Button
          variant="ghost"
          size="icon"
          class="text-chrome-foreground hover:bg-chrome-hover hover:text-white [@media(pointer:coarse)]:hidden"
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
    <ShortcutsDialog v-if="showShortcuts" @close="showShortcuts = false" />
  </SidebarProvider>
</template>
