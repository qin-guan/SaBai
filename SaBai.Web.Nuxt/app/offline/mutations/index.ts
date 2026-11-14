/**
 * The single place that loads every domain's offline mutations (and offline
 * prefetches). The API plugin imports this module at start-up, so every
 * definition is registered before a write queued before a reload is replayed.
 *
 * Each domain keeps its definitions at module top level in
 * `app/offline/mutations/<domain>.ts` and adds one import line here.
 */
import './villageCodes'
