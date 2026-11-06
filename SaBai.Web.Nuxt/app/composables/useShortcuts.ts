/**
 * Keyboard shortcut helpers.
 *
 *  - `useSaveShortcut(fn)`: Ctrl/⌘+S (and Ctrl/⌘+Enter) run `fn` while the
 *    calling component is mounted. The most recently mounted handler wins, so a
 *    dialog's form takes precedence over the page behind it.
 */
type Handler = () => void;
const saveHandlers: Handler[] = [];

export function runSaveShortcut(): boolean {
  const handler = saveHandlers[saveHandlers.length - 1];
  if (!handler) return false;
  handler();
  return true;
}

export function useSaveShortcut(handler: Handler) {
  onMounted(() => saveHandlers.push(handler));
  onBeforeUnmount(() => {
    const index = saveHandlers.lastIndexOf(handler);
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
