# SaBai.Web.Nuxt

Nuxt 4 port of `SaBai.Web` (Next.js). Same features and API calls, with a
utilitarian clinical UI modelled on EHR software (EPIC Hyperspace) and built on
shadcn-vue's **Lyra** style (`reka-lyra`: square corners, compact controls,
JetBrains Mono).

## Run

```bash
pnpm install                     # from the repo root (pnpm workspace)
pnpm --filter @sabai/sdk build   # the app imports the generated SDK
cp .env.example .env             # NUXT_API_URL → SaBai.API (server-side)
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
| `app/components/patient` | Patient banner, journey tracker, visit selector, station page frame, inline details form and edit dialog. |
| `app/composables` | Vue Query data hooks over `@sabai/sdk/clients`, village selection, shortcuts. |
| `app/utils` | Framework-free logic ported from SaBai.Web (formatting, vitals, splits, eyesight, dental mapping). |

Auth is an HttpOnly session cookie (`sabai.auth`) set by the API's
`POST /auth/login` (ASP.NET Core Identity) and passed through the `/api` proxy,
so no token is handled in JS. `middleware/auth.global.ts` asks `/auth/whoami`
who is signed in (cached by Vue Query), guards routes and admin-only pages
(`definePageMeta({ requiresAdmin: true })`), and any 401 returns to
`/login?redirectTo=…`. Login asks for an authenticator code when the account has
two-factor on. Admins manage staff under Settings → Staff Users
(`pages/settings/users.vue`: create, edit roles, set passwords, lock/unlock,
reset two-factor); everyone can change their password and set up two-factor
under My Account (`pages/account.vue`). The selected village is
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

## Patient journey and station backlog

Every visit moves through **Vitals → Vision → Consult → Dental (optional) →
Pharmacy (optional)**. The API returns where each visit is (`journey` on
`/patients/{id}/visits`, `lastVisitJourney` on `/patients/with-latest-visit`)
and `<JourneyTracker>` shows it the same way everywhere: done steps in green,
the next step highlighted, optional steps dashed. Every step links to that
station's form for the same visit, so a patient can be sent on from any screen.

- **`/patient` is the one patient list.** Unfiltered it shows everyone seen
  today and their journeys; a row opens the patient record. A station card
  (or a sidebar *Stations* link) filters it to
  `?station=vitals|vision|consults|dental|pharmacy`: who that station is
  waiting for, earliest arrival first; a row opens that station's form. Each row is one link, so
  there are no per-row buttons.
  `/vitals`, `/vision`, `/consults` and `/dental` redirect there; they used to
  be four near-identical lists.
- **Starting a visit** works without the camera too: Scan Face has a *Find
  patient* search (name, any form of the ID, ID number, phone or village — the
  same index as the universal search), and the patient record has a *New Visit*
  button. Both use `<StartVisitButton>`, which asks before opening a second
  visit for someone who already had one opened recently.
- **The patient record** (`/patient/:id`) shows the current visit's journey
  with a *Next* button, the journey of every past visit, and the patient's
  details, edited in place with a save bar like the station forms.
- **Station screens** show the journey bar under the patient banner.

Dental waits for patients who were referred or have finished their consult.
Pharmacy waits for undispensed orders.

### Wait estimates

Each station card shows how backlogged the station is: how many are waiting
and roughly how long someone sent there now would wait (waiting ÷ the
station's pace, `app/utils/waitTime.ts`). There are no per-patient estimates.
The pace comes from:

- **Measured throughput**: patients finished per minute in the last hour (or
  today, if the last hour has fewer than 3), from when each record was saved.
  The API stamps `RecordedAt` on vitals, eyesight and dental records; consults
  and dispensed orders already have timestamps. This includes however many
  staff work the station.
- **Fallback: form timing.** With too little throughput data, the estimate
  uses how long station forms take on this device: from the first key press or
  tap to the save, scaled up for a partly filled form
  (`app/utils/serviceTime.ts`, stored in `localStorage`, last 24 h).

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
