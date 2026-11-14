<script setup lang="ts">
/**
 * Plots height (blue) and weight (red) on the NCHS growth chart image for the
 * patient's age and gender. Pixel offsets are calibrated to each PNG.
 */
const props = defineProps<{
  age: number
  height: number | null
  weight: number | null
  gender: 'male' | 'female'
}>()

// Lowest values printed on the chart axes; anything below plots off-chart.
const MIN_HEIGHT_CM = 80
const MIN_WEIGHT_KG = 10

const CHART_CONFIG = {
  male: { src: '/boyhwgraph.png', xOrigin: 95, xStep: 23.5, weightOrigin: 765, weightScale: 4.52, heightOrigin: 607, heightScale: 4.52 },
  female: { src: '/girlhwgraph.png', xOrigin: 115, xStep: 22.67, weightOrigin: 742, weightScale: 4.25, heightOrigin: 593, heightScale: 4.25 },
} as const

const canvas = ref<HTMLCanvasElement>()

const hasValues = computed(
  () => props.height !== null && props.weight !== null && !Number.isNaN(props.height) && !Number.isNaN(props.weight),
)
const inRange = computed(
  () => hasValues.value && props.height! >= MIN_HEIGHT_CM && props.weight! >= MIN_WEIGHT_KG,
)

function drawRoundedRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  ctx.beginPath()
  ctx.moveTo(x + r, y)
  ctx.lineTo(x + w - r, y)
  ctx.quadraticCurveTo(x + w, y, x + w, y + r)
  ctx.lineTo(x + w, y + h - r)
  ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h)
  ctx.lineTo(x + r, y + h)
  ctx.quadraticCurveTo(x, y + h, x, y + h - r)
  ctx.lineTo(x, y + r)
  ctx.quadraticCurveTo(x, y, x + r, y)
  ctx.closePath()
}

function drawCross(ctx: CanvasRenderingContext2D, cx: number, cy: number, color: string, size = 3) {
  ctx.strokeStyle = color
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(cx - size, cy - size)
  ctx.lineTo(cx + size, cy + size)
  ctx.stroke()
  ctx.moveTo(cx + size, cy - size)
  ctx.lineTo(cx - size, cy + size)
  ctx.stroke()
}

function drawLabel(ctx: CanvasRenderingContext2D, cx: number, cy: number, text: string, color: string, crossSize = 3) {
  const W = 75
  const H = 25
  const x = cx + crossSize
  const y = cy - crossSize - H / 2
  ctx.fillStyle = 'rgba(255, 255, 255, 0.75)'
  drawRoundedRect(ctx, x, y, W, H, 5)
  ctx.fill()
  ctx.fillStyle = color
  ctx.font = '14px "Atkinson Hyperlegible Next", "Noto Sans Khmer", Arial, sans-serif'
  ctx.fillText(text, x + 5, cy - crossSize)
}

// The chart is always drawn, with the marks only when both values are in
// range, so the panel keeps the same size while a value is being typed (it
// used to swap a one-line hint for an 800px image as height passed 80 cm,
// pushing the rest of the form down mid-entry).
watchEffect((onCleanup) => {
  const el = canvas.value
  const ctx = el?.getContext('2d')
  if (!el || !ctx)
    return
  const { age, height, weight, gender } = props
  const plot = inRange.value
  const config = CHART_CONFIG[gender]
  // age axis starts at 2, weight axis at 10 kg, height axis at 80 cm
  const xPixel = config.xOrigin + (age - 2) * config.xStep
  const weightPixel = config.weightOrigin - (weight! - 10) * config.weightScale
  const heightPixel = config.heightOrigin - (height! - 80) * config.heightScale

  const image = new Image()
  image.crossOrigin = 'anonymous'
  image.src = config.src
  image.onload = () => {
    ctx.clearRect(0, 0, el.width, el.height)
    ctx.drawImage(image, 0, 0)
    if (!plot)
      return
    drawCross(ctx, xPixel, weightPixel, 'red')
    drawLabel(ctx, xPixel, weightPixel, `${weight} kg`, 'red')
    drawCross(ctx, xPixel, heightPixel, 'blue')
    drawLabel(ctx, xPixel, heightPixel, `${height} cm`, 'blue')
  }
  onCleanup(() => (image.onload = null))
})
</script>

<template>
  <p class="mb-2 text-xs text-muted-foreground touch:text-sm" aria-live="polite">
    <template v-if="!hasValues">
      Enter height and weight to plot them.
    </template>
    <template v-else-if="!inRange">
      The chart starts at {{ MIN_HEIGHT_CM }} cm and {{ MIN_WEIGHT_KG }} kg, so these values are not plotted.
    </template>
    <template v-else>
      Plotted {{ props.height }} cm and {{ props.weight }} kg at age {{ props.age }}.
    </template>
  </p>
  <!-- Dimensions match the PNG so drawImage fills the canvas exactly; the aspect ratio reserves the space before it draws. -->
  <canvas
    ref="canvas"
    width="600"
    height="800"
    class="aspect-[3/4] h-auto w-full"
    role="img"
    :aria-label="inRange
      ? `Growth chart: height ${props.height} cm and weight ${props.weight} kg at age ${props.age}`
      : 'Growth chart with no values plotted'"
  />
</template>
