# SaBai.Web.Nuxt

Nuxt 4 port of `SaBai.Web` (Next.js). Same features and API calls, with a
utilitarian clinical UI modelled on EHR software (EPIC Hyperspace) and built on
shadcn-vue's **Lyra** style (`reka-lyra`: square corners, compact controls,
JetBrains Mono).

## Run

```bash
pnpm install                     # from the repo root (pnpm workspace)
pnpm --filter @sabai/sdk build   # the app imports the generated SDK
cp .env.example .env             # NUXT_PUBLIC_API_URL → SaBai.API
pnpm dev
```

`pnpm test` runs the unit tests in `test/`; `pnpm typecheck` runs vue-tsc.

## Structure

| Path | What |
| --- | --- |
| `app/pages` | One page per SaBai.Web route (same URLs). |
| `app/layouts/default.vue` | App shell: navy app bar, activity sidebar, global keyboard shortcuts. |
| `app/components/ui` | shadcn-vue components (Lyra). Imported explicitly. |
| `app/components/app` | Building blocks: `AppPanel`, `PageHeader`, `FormField`, `ChoiceGroup`, `AppDialog`, `SaveBar`… |
| `app/components/patient` | Patient banner, worklist, visit selector, station page frame, edit dialog. |
| `app/composables` | Vue Query data hooks over `@sabai/sdk/clients`, village selection, shortcuts. |
| `app/utils` | Framework-free logic ported from SaBai.Web (formatting, vitals, splits, eyesight, dental mapping). |

Auth works as in SaBai.Web: the JWT from `POST /auth/login` is kept in the
`sabai_token` cookie, `middleware/auth.global.ts` guards routes, and any 401
clears the token and returns to `/login?redirectTo=…`. The selected village is
stored under the same `localStorage` key (`villageCodeState`).

## Keyboard

| Keys | Action |
| --- | --- |
| `Ctrl/⌘+K` or `/` | Universal search (see below). ↑/↓ move, Enter opens, Shift+Enter opens the patient record, Esc closes. |
| `Ctrl/⌘+S` | Save the current form or dialog. |
| `Enter` | In a form field: move to the next field (forms never submit on Enter). |
| `Ctrl/⌘+B` | Collapse / expand the sidebar. |
| `?` | Shortcut help. |
| `←`/`→` | Change the answer in a choice group (selecting the active answer again clears it). |
| `Esc` | Close a dialog, or cancel an inline table edit (Enter saves it). |

## Universal search

One box (header, `/` or `Ctrl/⌘+K`) for patients, sections and actions, built on
[MiniSearch](https://lucaong.github.io/minisearch/) (`app/utils/search.ts`):

- Patients match on name, any form of their ID (`12`, `0012`, `PC0012`),
  identification number, phone (separators ignored) and village; prefix
  matching plus typo tolerance for words (never for numbers).
- Sections match on clinical keywords too (`bp` → Vitals, `glasses` → Vision).
- With an empty query it lists recently opened patients.
- On a station page, Enter opens the patient in that station; each result also
  has Record / Vitals / Vision / Consult / Dental quick links.

## Drafts

Every clinical form (vitals, vision, consult, dental, registration, edit
patient) autosaves unsaved input to `localStorage` (`useFormDraft`), so a
reload or closed tab does not lose work. A restored draft is announced with a
"Discard draft" button; drafts clear on a successful save, expire after 24h,
and — together with recent patients — are wiped on sign-out because they hold
patient data.

## Phones

Below 640px (or on any touch screen) the dense desktop sizing steps up: body
text 15px, labels/hints 14px, inputs 16px (no iOS zoom-on-focus) and every
control 44px tall. Form grids collapse to one column, the sidebar becomes a
drawer, worklists become tappable rows, wide tables (stock, ingredients,
brands, village codes) stack into labelled cards, dialogs go full-screen and
the save bar sticks to the bottom with full-width buttons. Keyboard hints are
only shown on wide screens with a mouse.
