import type { Ref } from "vue";

/** A read-only ref that follows `source` after `delay` ms of quiet. */
export function useDebouncedRef<T>(source: Ref<T>, delay: number) {
  const debounced = ref(source.value) as Ref<T>;
  let timeout: number | undefined;
  watch(source, (value) => {
    window.clearTimeout(timeout);
    timeout = window.setTimeout(() => (debounced.value = value), delay);
  });
  onScopeDispose(() => window.clearTimeout(timeout));
  return debounced;
}
