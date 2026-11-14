<script setup lang="ts">
import { PhUser } from "@phosphor-icons/vue";

/**
 * A patient's photo (fetched through the authenticated API), or a placeholder
 * when they have none. `hasPhoto` skips the request when no photo exists.
 */
const props = withDefaults(
    defineProps<{
        patientId: string | number;
        hasPhoto?: boolean;
        size?: number;
        alt?: string;
        zoomable?: boolean;
        name?: string;
    }>(),
    { hasPhoto: false, size: 48, zoomable: false },
);
const zoomed = ref(false);
const url = usePatientPhoto(
    () => props.patientId,
    () => props.hasPhoto,
);
</script>

<template>
        <button
            v-if="url && props.zoomable"
            type="button"
            class="shrink-0 cursor-zoom-in focus-visible:outline-2 focus-visible:outline-ring"
            :aria-label="`Enlarge photo${props.name ? ` of ${props.name}` : ''}`"
            @click.stop.prevent="zoomed = true"
        >
            <img
                :src="url"
                alt=""
                :width="props.size"
                :height="props.size"
                class="border border-border bg-muted object-cover"
                :style="{ width: `${props.size}px`, height: `${props.size}px` }"
            />
        </button>
        <img
            v-else-if="url"
            :src="url"
            :alt="props.alt ?? 'Patient photo'"
            :width="props.size"
            :height="props.size"
            class="shrink-0 border border-border bg-muted object-cover"
            :style="{ width: `${props.size}px`, height: `${props.size}px` }"
        />
        <span
            v-else
            role="img"
            :aria-label="props.alt ?? 'No patient photo'"
            class="inline-flex shrink-0 items-center justify-center border border-border bg-muted text-muted-foreground"
            :style="{ width: `${props.size}px`, height: `${props.size}px` }"
        >
            <PhUser :size="Math.round(props.size * 0.5)" aria-hidden="true" />
        </span>

    <template v-if="zoomed && url">
        <AppDialog
            :title="props.name ?? 'Patient photo'"
            size="md"
            @close="zoomed = false"
        >
            <img
                :src="url"
                :alt="props.name ? `Photo of ${props.name}` : 'Patient photo'"
                class="mx-auto max-h-[70dvh] w-full object-contain"
            />
        </AppDialog>
    </template>
</template>
