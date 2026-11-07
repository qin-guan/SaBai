<script setup lang="ts">
import { PhCopy, PhDownloadSimple } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'

/** One-time recovery codes, shown once with copy and download helpers. */
const props = defineProps<{ codes: string[] }>()

async function copy() {
  try {
    await navigator.clipboard.writeText(props.codes.join('\n'))
    toast.success('Recovery codes copied.')
  }
  catch {
    toast.error('Could not copy. Select the codes and copy them by hand.')
  }
}

function download() {
  const blob = new Blob([`Sa'Bai recovery codes\n\n${props.codes.join('\n')}\n`], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = 'sabai-recovery-codes.txt'
  link.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="flex flex-col gap-2">
    <StateMessage
      kind="warning"
      message="Save these codes somewhere safe. Each one signs you in once if you lose your phone. They will not be shown again."
    />
    <ul class="grid grid-cols-2 gap-1 border border-border bg-stripe p-3 font-mono text-sm" aria-label="Recovery codes">
      <li v-for="code in props.codes" :key="code">
        {{ code }}
      </li>
    </ul>
    <div class="flex flex-wrap gap-2">
      <Button size="sm" variant="outline" @click="copy">
        <PhCopy aria-hidden="true" /> Copy
      </Button>
      <Button size="sm" variant="outline" @click="download">
        <PhDownloadSimple aria-hidden="true" /> Download
      </Button>
    </div>
  </div>
</template>
