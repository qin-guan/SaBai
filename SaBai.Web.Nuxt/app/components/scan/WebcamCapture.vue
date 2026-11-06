<script setup lang="ts">
import { PhCamera, PhCameraRotate, PhX } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'

/**
 * Camera capture with the same behaviour as SaBai.Web's WebcamInput:
 * opens automatically when there is no photo (unless `autoOpenCamera` is
 * false), closes once a photo is captured, and supports retake/cancel.
 * Capture stays disabled until the browser delivers a live stream.
 */
const image = defineModel<string | null>({ default: null })
const props = withDefaults(defineProps<{ size?: number, autoOpenCamera?: boolean, headGuide?: boolean }>(), {
  size: 250,
  autoOpenCamera: true,
  headGuide: true,
})
const root = ref<HTMLElement>()
const emit = defineEmits<{ cameraOpen: [open: boolean] }>()

const video = ref<HTMLVideoElement>()
const cameraIsOpen = ref(props.autoOpenCamera && !image.value)
const cameraReady = ref(false)
const cameraError = ref<string | null>(null)
const facingMode = ref<'environment' | 'user'>('environment')
let stream: MediaStream | null = null

function stopStream() {
  stream?.getTracks().forEach(t => t.stop())
  stream = null
  cameraReady.value = false
}

async function startStream() {
  stopStream()
  cameraError.value = null
  try {
    stream = await navigator.mediaDevices.getUserMedia({
      audio: false,
      video: { width: 720, height: 720, facingMode: facingMode.value },
    })
    if (!cameraIsOpen.value) {
      stopStream()
      return
    }
    await nextTick()
    if (video.value) {
      video.value.srcObject = stream
      await video.value.play().catch(() => {})
      cameraReady.value = true
    }
  }
  catch (error) {
    cameraReady.value = false
    cameraError.value = error instanceof Error ? error.message : String(error)
  }
}

watch(cameraIsOpen, (open) => {
  emit('cameraOpen', open)
  if (open)
    startStream()
  else stopStream()
}, { immediate: true })

// Close the camera when a photo arrives; reopen automatically when cleared.
watch(image, (value) => {
  if (value)
    cameraIsOpen.value = false
  else if (props.autoOpenCamera)
    cameraIsOpen.value = true
})

onBeforeUnmount(stopStream)

function openCamera() {
  cameraIsOpen.value = true
}

function switchCamera() {
  facingMode.value = facingMode.value === 'environment' ? 'user' : 'environment'
  startStream()
}

function capture() {
  const el = video.value
  if (!el || !cameraReady.value || !el.videoWidth) {
    toast.error(cameraError.value ?? 'Camera isn\'t ready yet. Allow camera access and try again.')
    return
  }
  const canvas = document.createElement('canvas')
  canvas.width = el.videoWidth
  canvas.height = el.videoHeight
  canvas.getContext('2d')?.drawImage(el, 0, 0)
  image.value = canvas.toDataURL('image/jpeg', 1)
}

// Keyboard: C captures, R / T opens the camera or retakes.
useKeyShortcut('c', capture, { enabled: () => cameraIsOpen.value && cameraReady.value, scope: root })
useKeyShortcut('r', () => { openCamera(); image.value = null }, { enabled: () => !cameraIsOpen.value && !!image.value, scope: root })
useKeyShortcut('t', openCamera, { enabled: () => !cameraIsOpen.value && !image.value, scope: root })
</script>

<template>
  <div ref="root" class="flex w-full flex-col items-center gap-2">
    <p class="text-xs font-semibold touch:text-sm">
      Photo<span class="text-destructive" aria-hidden="true">*</span>
    </p>
    <div
      class="relative flex aspect-square w-full items-center justify-center overflow-hidden border border-border bg-muted"
      :style="{ maxWidth: `${props.size}px` }"
    >
      <video
        v-if="cameraIsOpen"
        ref="video"
        autoplay
        playsinline
        muted
        class="size-full object-cover"
        aria-label="Camera preview"
      />
      <img v-else-if="image" :src="image" alt="Captured photo" class="size-full object-cover">
      <PhCamera v-else class="size-10 text-muted-foreground" aria-hidden="true" />
      <!-- Head guide: line the face up inside the oval, shoulders below it. -->
      <svg
        v-if="cameraIsOpen && props.headGuide"
        class="pointer-events-none absolute inset-0 size-full"
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
        aria-hidden="true"
        data-testid="head-guide"
      >
        <defs>
          <mask id="head-guide-mask">
            <rect width="100" height="100" fill="white" />
            <ellipse cx="50" cy="44" rx="23" ry="30" fill="black" />
          </mask>
        </defs>
        <rect width="100" height="100" fill="black" fill-opacity="0.35" mask="url(#head-guide-mask)" />
        <ellipse cx="50" cy="44" rx="23" ry="30" fill="none" stroke="white" stroke-width="0.8" stroke-dasharray="3 2" />
      </svg>
      <span
        v-if="cameraIsOpen && props.headGuide && cameraReady"
        class="pointer-events-none absolute inset-x-0 top-1 text-center text-xs text-white drop-shadow touch:text-sm"
      >Fit your face inside the oval</span>
      <span
        v-if="cameraIsOpen && !cameraReady && !cameraError"
        class="absolute inset-x-0 bottom-0 bg-black/60 px-2 py-1 text-center text-xs text-white touch:text-sm"
        role="status"
      >Starting camera…</span>
    </div>

    <p v-if="cameraIsOpen && cameraError" class="text-center text-xs text-destructive touch:text-sm" role="alert">
      Camera unavailable: {{ cameraError }}
    </p>

    <div class="flex flex-wrap items-center justify-center gap-2">
      <template v-if="cameraIsOpen">
        <Button variant="success" :disabled="!cameraReady" @click="capture">
          <PhCamera aria-hidden="true" /> Capture <ShortcutHint class="hidden kb:inline-flex" keys="C" />
        </Button>
        <Button variant="outline" aria-label="Switch camera" :disabled="!cameraReady && !cameraError" @click="switchCamera">
          <PhCameraRotate aria-hidden="true" />
        </Button>
        <!-- No cancel before a photo exists: there is nothing to go back to. -->
        <Button v-if="image" variant="destructive" @click="cameraIsOpen = false">
          <PhX aria-hidden="true" /> Cancel
        </Button>
      </template>
      <Button v-else-if="!image" variant="success" @click="openCamera">
        <PhCamera aria-hidden="true" /> Take Photo <ShortcutHint class="hidden kb:inline-flex" keys="T" />
      </Button>
      <Button
        v-else
        variant="destructive"
        @click="() => { openCamera(); image = null }"
      >
        <PhCameraRotate aria-hidden="true" /> Retake Photo <ShortcutHint class="hidden kb:inline-flex" keys="R" />
      </Button>
    </div>
  </div>
</template>
