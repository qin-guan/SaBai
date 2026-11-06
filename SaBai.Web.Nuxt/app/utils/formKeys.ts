/**
 * Enter-key handling for long clinical forms.
 *
 * Browsers submit a form when Enter is pressed in a single-line field
 * ("implicit submission"). On a 30-field form that saves half-entered data by
 * accident, so instead Enter moves to the next field (EHR-style), and saving
 * stays an explicit action: the Save button or Ctrl/⌘+S.
 *
 * Usage: <form @keydown.enter="enterMovesToNextField">
 */
export function enterMovesToNextField(event: KeyboardEvent) {
  const target = event.target
  if (!(target instanceof HTMLInputElement))
    return
  if (['submit', 'button', 'checkbox', 'radio', 'file', 'color'].includes(target.type))
    return
  if (event.ctrlKey || event.metaKey || event.altKey || event.isComposing)
    return
  event.preventDefault()

  const form = target.form
  if (!form)
    return
  const fields = [...form.querySelectorAll<HTMLElement>('input, select, textarea, button[role="radio"], [data-slot="toggle-group-item"]')]
    .filter(el => !el.hasAttribute('disabled') && el.offsetParent !== null && el.tabIndex >= 0)
  const next = fields[fields.indexOf(target) + (event.shiftKey ? -1 : 1)]
  next?.focus()
  if (next instanceof HTMLInputElement && next.type !== 'date')
    next.select()
}
