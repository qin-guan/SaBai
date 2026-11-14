# SaBai.Web.Nuxt

Nuxt 4 port of `SaBai.Web` (Next.js). Same features and API calls, with a
utilitarian clinical UI modelled on EHR software (EPIC Hyperspace) and built on
shadcn-vue's **Lyra** style (`reka-lyra`: square corners, compact controls).
Text is set in **Atkinson Hyperlegible Next**, with **Atkinson Hyperlegible
Mono** for IDs and codes: both were drawn so look-alike characters (1/l/I, 0/O,
5/S) never get confused, which matters for patient IDs, doses and readings.

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
| `app/composables` | Vue Query hooks over `@sabai/sdk`, `useOfflineMutation`, `useSyncState`, village selection, shortcuts. |
| `app/offline` | Offline core: outbox, replay, connectivity, server time, query-cache persistence, prefetch, the SDK transport. `mutations/` holds each domain's offline writes. |
| `app/service-worker` | Service worker of the installed PWA: Workbox app-shell precaching, SPA offline navigation, runtime asset caching, and Web Push. |
| `app/utils` | Framework-free logic ported from SaBai.Web (UUIDv7 generator, formatting, vitals, splits, eyesight, dental mapping). |

## Offline

The REST API is the only read and write path; offline support is in the client
(`app/offline`, unit-tested in `test/offline`).

- **Reads**: the Vue Query cache is saved to IndexedDB and restored before the
  app renders (`queryPersistence.ts`), kept for 7 days and dropped when the
  build changes. A failed refetch keeps showing the cached data. Queries with
  `meta: { persist: false }` are not saved. Each domain registers what to
  download for offline use (`registerOfflinePrefetch`); it runs after sign-in
  (at most every 30 minutes) and from *Download for offline use*.
- **Writes**: a write that cannot reach the server goes into the **outbox**
  (IndexedDB) and is replayed in order when the connection is back. The UI is
  updated at once by editing the query cache. Replayed writes carry
  `If-Unmodified-Since` (the server's `Date` from this device's last successful
  response before the write was made); a `412` puts the item in *conflict*,
  another `4xx` *rejects* it, and later writes for the same record wait behind
  it. `/settings/sync` lists pending items and offers *Overwrite* (resend
  without the header), *Retry* and *Discard*. Discarding a waiting change also
  takes it off the screen at once, offline too (each item keeps the cached data
  its edit replaced; the other waiting changes are applied again on top).
- **Per user**: each queued write belongs to the staff user who made it and is
  only sent while they are signed in. If someone else signs in on the device
  (e.g. after a session expired; a `401` during replay sends the user to the
  login page), the cached data of the previous user is dropped and their
  writes stay parked — listed on `/settings/sync`, discardable by an admin —
  until they sign in again.
- **Connection**: decided by real request outcomes (fetch failure, timeout,
  `502/503/504` from the proxy), not `navigator.onLine`, plus a probe of
  `/api/healthcheck` while offline or while writes are waiting.
- **Sign-out** warns about the user's unsent writes, then deletes them and the
  saved cache (they hold patient data). Other users' parked writes stay.

To make a write work offline:

1. Define it once at module top level in `app/offline/mutations/<domain>.ts`
   with `defineOfflineMutation({ key, describe, send, optimistic, onSuccess, recordIds })`,
   and import that file from `app/offline/mutations/index.ts`.
2. Put everything `send` needs in `vars` (they are stored as JSON). For a create,
   generate the id on the client with `uuidv7()` and send it in the body (the
   API's create endpoints accept it and are idempotent on it).
3. In `send`, spread `...requestOptions(ctx)` into the SDK call so replays carry
   `If-Unmodified-Since`.
4. Write `optimistic` as idempotent, immutable cache edits by id (upsert/remove)
   into every query a following screen reads.
5. In the component: `const { mutateAsync } = useOfflineMutation(def)`, then
   `notifySaved(await mutateAsync(vars), '… saved')`. Server errors are thrown
   as before. `app/offline/mutations/villageCodes.ts` is the worked example.

The service worker precaches the app shell, so the app opens offline. Nitro
proxies `/api/*` to `NUXT_API_URL` (`SaBai.API`).

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
| `Esc` | Close a dialog, cancel an inline table edit (Enter saves it), or go back to the parent page. |
| `↑`/`↓` | Referrals list: move between referrals (↓ from the search box enters the list); Enter opens one, and Esc on it returns to the same row. |
| `1`–`4` | Referral: go to the booth, party, payment or outcome step. |

The referral page is one form like the station forms: status, booth, party,
payment and outcome are always editable and `Ctrl/⌘+S` saves every part that
changed (each through its own endpoint, so only changed parts are sent).

## Patient journey and station backlog

Every visit moves through **Vitals → Vision → Consult → Dental (optional) →
Pharmacy (optional)**. The API returns where each visit is (`journey` on
`/patients/{id}/visits`, `lastVisitJourney` on `/patients/with-latest-visit`)
and `<JourneyTracker>` shows it the same way everywhere: done steps in green,
the next step highlighted, optional steps dashed. Every step links to that
station's form for the same visit, so a patient can be sent on from any screen.

- **`/patient` is the one patient list.** Unfiltered it shows every patient and
  their latest visit's journey; a row opens the patient record. A station card
  (or a sidebar *Stations* link) opens `?station=vitals|vision|consults|dental|pharmacy`
  with two sections: **Waiting for `<Station>`** on top (earliest arrival
  first) and a collapsible **Completed & other patients** section below, with no
  calendar-day split so clinics running past midnight are unaffected; a row
  opens that station's form. Each row is one link, so there are no per-row
  buttons. `/vitals`, `/vision`, `/consults` and `/dental` redirect there; they
  used to be four near-identical lists.
- **Starting a visit** works without the camera too: Scan Face has a *Find
  patient* search (name, any form of the ID, ID number, phone or village — the
  same index as the universal search), and the patient record has a *New Visit*
  button in its banner. Both use `<StartVisitButton>`, which asks before opening a second
  visit for someone who already had one opened recently.
- **The patient record** (`/patient/:id`) shows the current visit's journey
  (each step opens that station), the journey of every past visit, and the
  patient's details, edited in place with a save bar like the station forms.
- **Station screens** show the journey beside the visit picker, under the
  patient banner.

The journey is five equal cells (done in green, the next step filled, optional
steps dashed), each a link to that station for the visit. On station screens
and the patient record it starts collapsed to a one-line summary ("Next:
Vision · 1 of 3 done").

## Collapsed sections

Reference and good-to-have sections start collapsed so screens show the work
first: the station journey, the record's current visit and visit history,
profile photo, previous consults, growth charts, and the optional vitals notes
(urinalysis, remarks — open when they already hold text). The header keeps a
one-line summary; opening or closing one is remembered on the device
(`AppPanel collapsible collapse-key="…"`, `usePersistedToggle`). Collapsed
content stays mounted, so typed input is kept.

## Drug allergies

The free-text answer is split into separate allergies
(`parseDrugAllergies`: commas, semicolons, slashes, new lines, "and"). Lists
show a small solid red tag with the first allergy and a "+N" count (an
*Allergies* column on laptops). The patient banner ends in a full-width red
band, like an allergy wristband, listing every allergy as plain text — never
hidden behind a click. "None"-style answers show a quiet "No known drug
allergies" line; an empty answer shows an amber prompt to ask the patient.

Dental waits for patients who were referred or have finished their consult.
Pharmacy waits for undispensed orders.

### Wait estimates

Each station card shows how backlogged the station is: how many are waiting
and roughly how long someone sent there now would wait (waiting ÷ the
station's pace, `app/utils/waitTime.ts`). There are no per-patient estimates.
The pace comes from:

- **Measured throughput**: patients finished per minute in the last hour (or
  the last 12 hours, if the last hour has fewer than 3), from when each record
  was saved.
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

## Installable app and notifications

The app is an installable PWA (`@vite-pwa/nuxt`, `pwa` in `nuxt.config.ts`) so
that notifications can reach phones and desktops.

- **Manifest and icons** (`/manifest.webmanifest`, `public/pwa-*.png`,
  `apple-touch-icon.png`): the app installs from Chrome/Edge (desktop and
  Android) and from Safari's *Add to Home Screen* / *Add to Dock*. iOS and
  iPadOS deliver Web Push only to the Home Screen copy (16.4+).
- **Service worker** (`app/service-worker/sw.ts`): shows a notification for
  every push message and opens the app at the message's `url` when it is
  tapped. It also precaches the app shell and caches patient photos (see
  Offline); other `/api` requests pass through untouched. It also runs under
  `pnpm dev`.
- **Message format** (`app/utils/pushPayload.ts`, `PushNotification` in the
  API): a JSON body `{ title, body?, url?, tag?, requireInteraction? }`. `url`
  must be a path in this app; anything else opens the home page.
- **Turning them on** (My Account → Notifications,
  `components/account/NotificationSettings.vue`, `usePushNotifications`): a
  button asks the browser for permission and subscribes the device, with a
  test notification and a way to turn them off. It says why when they cannot
  work: an iPhone/iPad tab that is not on the Home Screen, a blocked
  permission, an unsupported browser, or a server without keys. Notifications
  are per device and belong to whoever is signed in: signing out turns them
  off on that device.
- **Sending them** (Settings → Notifications, `pages/settings/notifications.vue`,
  `useBroadcasts`): a screen to compose clinic announcements with title, severity
  (info, warning, critical) and message, which publishes the broadcast and triggers
  `PushNotifier.NotifyEveryoneAsync` to deliver Web Push notifications to all
  registered staff devices, alongside a history table of past broadcasts.
- **API** (`SaBai.API/Endpoints/Push`, `SaBai.API/Endpoints/Broadcasts`, `SaBai.API/Push`):
  `GET /push/config` (public key, whether push is on), `PUT /push/subscriptions`,
  `POST /push/subscriptions/unsubscribe`, `POST /push/test`, `GET /broadcasts`,
  `POST /broadcasts`, and `POST /broadcasts/{id}/publish`. Push subscriptions are
  called through `app/utils/pushApi.ts`, while broadcasts use `@sabai/sdk`. Publishing
  a broadcast pushes it to all staff devices via `PushNotifier.NotifyEveryoneAsync`.

Notifications stay off until the API has a VAPID key pair. Generate one with
`npx web-push generate-vapid-keys` and set `Push__PublicKey`, `Push__PrivateKey`
(a secret) and `Push__Subject` (a `mailto:` address or `https://` URL) on the
API; in the Aspire deployment these are the `pushPublicKey`, `pushPrivateKey`
and `pushSubject` parameters. Changing the keys later means every device has to
turn notifications on again. Push needs HTTPS (or `localhost`), and the API
needs outbound HTTPS to the browsers' push services. Devices may only subscribe
through the push services listed in `PushSettings` (Google, Mozilla, Microsoft,
Apple); override with `Push__AllowedHosts__0`, `__1`, … if a browser uses another.

## Screen sizes

- **Laptops (13–15")**: the patient list is a column grid (patient, ID,
  age/sex, arrival time and time in clinic, journey) with ~40px rows, and data
  tables use ~36px rows. Station screens put the visit picker and journey in
  one strip under the patient banner so the form starts higher. Text uses 13px
for compact UI text and 14.5px for body text.
- **Tablets**: below 1100px the sidebar starts as an icon rail (Ctrl/⌘+B or the
  toggle expands it; the choice is remembered). The patient list folds ID, age
  and arrival into one line under the name, with the journey beside it.

## Phones

Below 640px (or on any touch screen) the dense desktop sizing steps up: body
text 15px, labels/hints 14px, inputs 16px (no iOS zoom-on-focus) and every
control 44px tall. Form grids collapse to one column (short numeric vitals stay two
per row), the app bar shows the section name, a village chip (colour and code) and a
search icon instead of the search box, the sidebar becomes a
drawer, worklists become tappable rows, wide tables (stock, ingredients,
brands, village codes) stack into labelled cards, dialogs go full-screen and
the save bar sticks to the bottom with full-width buttons. Keyboard hints are
only shown on wide screens with a mouse.
