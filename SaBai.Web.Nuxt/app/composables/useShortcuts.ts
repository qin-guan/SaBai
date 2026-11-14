/**
 * Keyboard shortcut helpers.
 *
 *  - `useSaveShortcut(fn)`: Ctrl/⌘+S (and Ctrl/⌘+Enter) run `fn` while the
 *    calling component is mounted. The most recently mounted handler wins, so a
 *    dialog's form takes precedence over the page behind it.
 */
type Handler = () => void;
interface SaveEntry {
  handler: Handler;
  enabled?: () => boolean;
}
const saveHandlers: SaveEntry[] = [];

export function runSaveShortcut(): boolean {
  // Newest first; entries whose `enabled` gate is closed are skipped.
  for (let i = saveHandlers.length - 1; i >= 0; i--) {
    const entry = saveHandlers[i]!;
    if (entry.enabled && !entry.enabled()) continue;
    entry.handler();
    return true;
  }
  return false;
}

/**
 * `scope` limits the shortcut to while focus is inside that element (for pages
 * with several independent forms, e.g. accordion sections); `enabled` gates it
 * on any other condition.
 */
export function useSaveShortcut(
  handler: Handler,
  options: { enabled?: () => boolean; scope?: Ref<HTMLElement | undefined | null> } = {},
) {
  const entry: SaveEntry = {
    handler,
    enabled: () =>
      (!options.scope || !!options.scope.value?.contains(document.activeElement))
      && (!options.enabled || options.enabled()),
  };
  onMounted(() => saveHandlers.push(entry));
  onBeforeUnmount(() => {
    const index = saveHandlers.indexOf(entry);
    if (index >= 0) saveHandlers.splice(index, 1);
  });
}

/** True when the event target is somewhere the user is typing. */
export function isTypingTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) return false;
  return (
    target.isContentEditable ||
    target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement
  );
}

export const isMac = () =>
  typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.platform);

/**
 * Runs `fn` when a bare key (no Ctrl/⌘/Alt) is pressed outside text fields.
 * While a dialog is open it only fires for components inside that dialog
 * (pass the component's root element as `scope`). `enabled` can gate it.
 */
export function useKeyShortcut(
  key: string,
  fn: () => void,
  options: { enabled?: () => boolean; scope?: Ref<HTMLElement | undefined | null> } = {},
) {
  function onKeydown(event: KeyboardEvent) {
    if (event.key.toLowerCase() !== key.toLowerCase()) return;
    if (event.ctrlKey || event.metaKey || event.altKey || event.repeat) return;
    if (isTypingTarget(event.target)) return;
    if (options.enabled && !options.enabled()) return;
    const dialog = document.querySelector('[role="dialog"]');
    if (dialog && !(options.scope?.value && dialog.contains(options.scope.value))) return;
    event.preventDefault();
    fn();
  }
  onMounted(() => window.addEventListener("keydown", onKeydown));
  onBeforeUnmount(() => window.removeEventListener("keydown", onKeydown));
}
