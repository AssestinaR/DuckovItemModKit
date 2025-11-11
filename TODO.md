# IMK TODO (bus-oriented refactor plan)

Vision
- Shift to a bus-oriented core: small, single-responsibility services composed by a facade.
- TreeData clone becomes a first-class strategy focused on structured clone/serialize only.

Sprint backlog (now)
1) Services and contracts
   - Define `ITreeDataCloneService` (Clone/Export/Import, no placement/refresh)
   - Define `IInventoryPlacementService` (Add/IndexOf/Verify/Retry policy)
   - Define `IVariableMergeService` (none|onlyMissing|overwrite)
   - Define `IUIRefreshService` (NeedInspection + Refresh)
   - Define `ICloneDiagnostics` (timings, counts, strategyUsed, retries, degraded)
2) Facade & options
   - Add `IMKDuckov.Clone.CloneTreeAsync(source, CloneOptions)`
   - `CloneOptions`: strategy(TreeData|Unity|Auto), variableMerge, copyTags, target(character|storage|explicit), retryPolicy, diagnostics
   - `CloneResult`: newItem, placement info, diagnostics, strategyUsed, degraded
3) TreeData module slimming
   - Keep: `FromItem/InstantiateAsync` wrapping + precise CustomData serialize
   - Remove/move out: inventory resolve, UI refresh, diagnostics, delay logic
   - Centralize reflection/delegate cache for TreeData/CustomData
4) Placement unify
   - Post-clone verify: AddAndMerge ¡ú IndexOf/InInventory ¡ú retry (next-frame) ¡ú optional target switch
   - Fire `PublishItemAdded` and call UI refresh via `IUIRefreshService`
5) Rebirth/Mover/Write split
   - Rebirth: generate new + map meta only; delegate placement/UI refresh/persistence
   - Mover: split pure inventory moves vs player send/warehouse ops
   - WriteService: split into CoreFields/Variables/Modifiers/Slots; keep Transactions independent
6) Await & lifecycle
   - Provide unified awaiter for UniTask (`IAwaiter` or helper) to avoid busy-wait
   - Align phases: Capture ¡ú Instantiate ¡ú VariableMerge/Appliers ¡ú Placement ¡ú Refresh
7) Inventory resolver
   - `IInventoryResolver`: character/storage/current-UI/explicit handle, avoid ad-hoc reflection

Diagnostics & safety
- Add opt-in diagnostics (off|warn|detail) for clone/placement sampling
- Add structured logs: entries, varCount, timings, target, result index, retries
- Suspend event burst during clone; publish coalesced events afterwards

Performance
- Pre-warm reflection caches; prefer CreateDelegate for hot paths
- Define size/time thresholds for diagnostics sampling

Compatibility & fallback
- TreeData failure ¡ú UnityClone + diff variable merge ¡ú mark degraded in result
- Maintain legacy API shims for one transition version

Documentation
- Author guide: bus architecture, responsibilities, strategy selection, persistence expectations
- Samples: keep advanced lab features behind a debug flag

Done (recent)
- Added advanced TreeData clone with detailed diagnostics in Samples
- Fixed route matching for `/item/cloneTreeAdvToBag` (no longer shadowed by StartsWith)
- Added UI lab page and inventory diagnostics; target=character/storage selector
- Fixed web `app.js` missing helpers (`nodeKey`, open-set persistence)

Backlog (kept from legacy TODO)
- Deduplicate Effects on apply (skip if same type already present)
- Transaction API: Commit(flushImmediate)
- Scheduler adaptive MaxPerTick when backlog is large
- Add flush JSON size + duration logging toggle
- SuperStress: expose config (waves, mods, seed) via UI fields
- Add FormatVersion bump path for future stat schema
- Add Reset() on PersistenceScheduler for profile switches
- Incremental stat delta capture (deferred)
- MetaEnricher sample (prune large arrays)
- Unit tests harness (editor-friendly)

Removed (no longer applicable per product direction)
- Optional compression (LZ4) gated by size threshold (user opted out)
- Validation features (user opted out for production; keep only as diagnostics, not default)
