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
| `/` | Search patients (↑/↓ to move, Enter to open, Esc to clear). On a station page, results open that station. |
| `Alt+1` … `Alt+9` | Switch activity (Scan Face, Patient, Vitals, Vision, Consults, Dental, Medication Stock, Orders, Settings). |
| `Ctrl/⌘+S` | Save the current form or dialog. |
| `Ctrl/⌘+B` | Collapse / expand the sidebar. |
| `?` | Shortcut help. |
| `←`/`→` | Change the answer in a choice group (selecting the active answer again clears it). |
| `Esc` | Close a dialog, or cancel an inline table edit (Enter saves it). |

## Phones

The sidebar becomes a drawer, worklists become tappable rows, dialogs go
full-screen, the save bar sticks to the bottom of the screen, and on touch
devices controls grow to 44px with 16px text (no iOS zoom-on-focus).
