# WopiHost.CellBridge

> [!WARNING]
> **Experimental.** This package wires the open-source [cellbridge](https://github.com/PatrickMatthiesen/cellbridge) MS-FSSHTTP engine into WopiHost as an alternative to the proprietary [`WopiHost.Cobalt`](../WopiHost.Cobalt/README.md). It compiles and its unit tests drive real MS-FSSHTTPB requests through the WOPI seam, but it has **not** been exercised against Office Online Server — see [What is still unknown](#what-is-still-unknown). `WopiHost.Cobalt` remains the production path.

[cellbridge](https://github.com/PatrickMatthiesen/cellbridge) (MIT, .NET 10, by [Patrick Matthiesen](https://github.com/PatrickMatthiesen)) implements [MS-FSSHTTP](https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttp/) (the SOAP/MTOM cell-storage envelope), [MS-FSSHTTPB](https://learn.microsoft.com/openspecs/sharepoint_protocols/ms-fsshttpb/) (the binary storage index / data-element / knowledge graph) and the chunking that shreds Office packages into reusable parts — the whole stack that `Microsoft.CobaltCore.dll` provides to `WopiHost.Cobalt`. It was written so desktop Word/Excel/PowerPoint can open, edit and save documents against a plain ASP.NET Core server the way they do against SharePoint; this package reuses its protocol engine behind WopiHost's existing `ICobaltProcessor` seam.

## How it fits

```
Office Online Server ──X-WOPI-Override: COBALT──▶ WopiHost.Core (POST /wopi/files/{id})
                                                       │  ICobaltProcessor.ProcessCobalt(file, principal, body)
                                                       ├── WopiHost.Cobalt     → Microsoft.CobaltCore (proprietary)
                                                       └── WopiHost.CellBridge → CellBridge.AspNetCore + FssHttpB + Storage (MIT)
```

* `ICobaltProcessor` (in `WopiHost.Abstractions`) is the only contract the WOPI endpoint depends on, so the two backends are drop-in alternatives and exactly one is registered per host.
* `CellBridgeProcessor` imports each WOPI file into cellbridge's document store on first use (`CellBridgeDocumentService.ImportAsync`), executes the request through `CellBridgeDocumentService.ExecuteAsync` / `FssHttpLockCoordinator`, and when a save advances cellbridge's content version it copies the materialized Office package back out through `IWopiWritableFile.OpenWriteAsync()` so `GetFile` and non-Cobalt clients see the result. This mirrors what `CobaltProcessor` does with `CobaltFile` + `GenericFda`.
* State lives in cellbridge's **in-memory** stores (`InMemoryStateStore` / `InMemoryContentStore`), the same single-process posture as `WopiHost.Cobalt`'s `LocalHostBlobStore`. cellbridge also ships PostgreSQL and filesystem stores for multi-instance deployments; swapping them in is a constructor change.
* Authorization stays with WOPI: the `COBALT` endpoint already requires the token's update permission, so `WopiCellBridgeAccessEvaluator` grants read/write inside cellbridge unconditionally instead of duplicating grants there.

## Enable it

The adapter consumes cellbridge's prerelease NuGet packages (`CellBridge.AspNetCore` and `CellBridge.Storage.InMemory`; versions in `Directory.Packages.props`, bump both together) and builds with the rest of the solution — no checkout, no private feed:

```sh
dotnet build WOPI.slnx
dotnet test --project test/WopiHost.CellBridge.Tests/WopiHost.CellBridge.Tests.csproj
```

Then in the sample host:

```jsonc
{
  "Sample": { "CoauthoringProvider": "CellBridge" },   // None | CobaltCore | CellBridge
  "Wopi": {
    "CellBridge": {
      "SerializationProfile": "SharePoint13_11",       // or "Current" (MS-FSSHTTPB v12 framing)
      "WebOrigin": ""                                  // WebUrl attribute on SOAP responses, if that framing is in use
    }
  }
}
```

or in your own host:

```csharp
builder.Services.AddCellBridgeProcessor(builder.Configuration);
```

When `ICobaltProcessor` is registered, `WopiHost.Core` advertises `SupportsCobalt` / `SupportsCoauth` in `CheckFileInfo` and routes `X-WOPI-Override: COBALT` bodies to it.

## Try cellbridge itself

cellbridge's own demo (desktop Office editing against its document library) runs from the WopiHost AppHost with `AppHost:UseCellBridge=true`: a PostgreSQL container, schema init, a seeded `integration-writer` account (`AppHost:CellBridgeTestPassword`) and the library at `https://localhost:7292/library`. Windows desktop Office must trust the ASP.NET Core dev certificate; see [cellbridge's demo guide](https://github.com/PatrickMatthiesen/cellbridge/blob/main/docs/demo-library.md). This lane demonstrates cellbridge, not WopiHost + cellbridge: nothing Docker-distributable speaks Cobalt over WOPI.

The demo's host and tools (`CellBridge.Web`, `CellBridge.Demo`, `CellBridge.Storage.Setup`, `CellBridge.Admin`) are not packaged, so the lane builds them from a source checkout:

```sh
# next to the WopiHost checkout, so the two trees are siblings
git clone https://github.com/PatrickMatthiesen/cellbridge ../cellbridge
dotnet build WOPI.slnx            # IncludeCellBridgeDemo auto-detects ../cellbridge/CellBridge.slnx
```

`IncludeCellBridgeDemo` (root `Directory.Build.props`) can be forced with `-p:IncludeCellBridgeDemo=true|false`; `-p:CellBridgeRepoRoot=<path>` points elsewhere. The checkout must stay **outside** the WopiHost tree, otherwise it inherits WopiHost's central package management and fails to restore. Without it the AppHost compiles without the lane. `.github/workflows/cellbridge-integration.yml` builds the lane weekly against the cellbridge commit the packages were built from (`CELLBRIDGE_PINNED_REF`); move the pin and the package versions together.

## What is still unknown

1. **The WOPI `COBALT` body framing.** `ICobaltProcessor` receives bytes without a Content-Type. cellbridge parses SOAP (what desktop Office posts to `cellstorage.svc`) and raw MS-FSSHTTPB cell requests; `Microsoft.CobaltCore` reads its own `RequestBatch` wire format, which carries lock/co-auth/editors subrequests *and* cell requests in one binary batch. `WopiCobaltFraming` sniffs for the two framings cellbridge understands and the processor throws `NotSupportedException` (logging the first bytes) for anything else. **The first milestone is capturing a real OOS `COBALT` request** — set the sample's logging to `Debug` and hit the endpoint from an OOS/OWA 2013 session — and teaching cellbridge (or this adapter) that framing. This was also the first step the [#321](https://github.com/petrsvihlik/WopiHost/issues/321) analysis called for.
2. **MTOM over WOPI.** If OOS posts `multipart/related`, the boundary lives in the Content-Type header the current `ICobaltProcessor` signature drops; the contract would need to carry it.
3. **Subrequests this adapter still dispatches itself.** `EditorsTable`, `GetDocMetaInfo` and `GetVersions` answer `NotSupported` here. cellbridge `0.1.0-beta.2` added `CellBridgeRequestProcessor`, which executes a parsed `CellStorageRequest` without an `HttpContext`; the SOAP path here should delegate to it instead of switching on subrequest types.
4. **Eviction.** Imported documents stay in memory for the process lifetime; `CobaltProcessor` evicts idle sessions after 60 minutes. beta.2's `IDocumentLifecycleStore.TryDeleteAsync` deletes a document under generation checks, which fits WOPI `DELETE`; dropping an idle document from memory without deleting it is a different operation and still needs a policy here.
5. **Two-desktop co-authoring** is unverified in cellbridge itself (single-desktop Word/Excel/PowerPoint save + reopen is).

## How the two sets of abstractions line up

Side by side, with what the first integration showed about each seam:

| Concern | WopiHost | cellbridge | Fit |
|---|---|---|---|
| Published document bytes | `IWopiWritableFile.OpenReadAsync` / `OpenWriteAsync` — one mutable stream per file, committed on dispose | `IContentStore` — immutable, content-addressed blobs (`WriteAsync(stream) → ContentHandle{Sha256}`), used for the materialized package **and** every graph element payload | Half. The materialized package maps cleanly (this adapter mirrors it on publish). Graph payloads are protocol state WopiHost has no slot for and should not grow one — `WopiHost.Cobalt` keeps CobaltCore's `HostBlobStore` private for the same reason. |
| Protocol state (storage index, knowledge, editors, leases, receipts) | none — `WopiHost.Cobalt` keeps `CobaltFile` sessions and the editors table in process memory | `IDocumentStateStore.TransitionAsync(id, (current, now) => …)` — atomic publish of an immutable `DocumentState` under per-document coordination with authoritative time | No counterpart, and cellbridge's model is the stronger one: a durable, multi-instance co-authoring state store is something WopiHost gains by adopting it rather than wrapping it. |
| Locks | `IWopiLockProvider` — one lock id per file, 30-minute expiry, CAS (`TryUnlockAndRelockAsync`, `RefreshLockAsync(expected)`) | `CoordinationState` — a schema lock shared by co-authors, an exclusive lease, coauthor transitions | Overlapping but disjoint today: WOPI `PutFile` honours `IWopiLockProvider` while FSSHTTP saves honour the lease — true with CobaltCore as well (`CobaltHostLockingStore` answers every lock request with an empty success). A cellbridge exclusive lease that consults `IWopiLockProvider` would be the first time both save paths share one lock domain. The most valuable unification target. |
| Document identity | opaque string ids (`IWopiResource.Identifier`, SHA-256 of a canonical path) | `ResourceId` GUID plus URL `PathKey`; beta.2's `ImportAsync` / `CreateAsync` overloads accept a host-supplied GUID | This adapter still keeps a map. Deriving the GUID from `IWopiResource.Identifier` and passing it in removes the map and survives restarts with a durable state store. |
| Caller identity | `ClaimsPrincipal` from the WOPI access token (`NameIdentifier`, `Name`) | `CellBridgeActor.FromPrincipal` over `cellbridge:subject` / `cellbridge:display-name` claims | Clean. `ActorFor` maps `NameIdentifier` to `wopi:<id>`. |
| Permissions | capability-style: `wopi:fperms` baked into the token at mint time, read through `IWopiPermissionProvider.GetFilePermissionsAsync(principal, file)` (async) | ACL-style: `DocumentSecurity` grants stored on the document, evaluated by `ICellBridgeAccessEvaluator.Evaluate(actor, state)` (sync, inside transactions) | Different philosophies, one seam. The token is WOPI's source of truth, so the evaluator defers to it; today it grants read/write unconditionally because the `COBALT` endpoint already requires the token's update permission. Having `QueryAccess` report read-only to a viewer needs the host's decision to travel with the request — which is what beta.2's `CellBridgeActor.AccessLimit` (a per-request ceiling scoped to one resource) and `ICellBridgeAuthorizationPolicy` (host-owned, synchronous, snapshots resolved outside transactions) provide. Not adopted here yet. |
| Sign-in | WOPI access token + proof keys; Office Online Server never sees a login page | MS-OFBA forms sign-in for desktop Office (`CellBridge.Authentication`) | Not applicable over WOPI; stays out. |

What a real unification needs from cellbridge, in priority order, and what `0.1.0-beta.2` already answers. The adapter was written against an earlier commit and adopts none of these yet; that is the next step.

1. A transport-agnostic request processor (SOAP or binary request model in, response model out, no `HttpContext`), so `EditorsTable`, `GetDocMetaInfo`, `GetVersions` and the Coauth transitions are reachable from a host that owns its routes and sign-in. **beta.2:** `CellBridgeRequestProcessor.ExecuteAsync(CellStorageRequest, …)` returns the response plus the accepted saves.
2. Host-supplied document identity, or a documented deterministic derivation. **beta.2:** `ImportAsync(Guid resourceId, …)` / `CreateAsync(Guid resourceId, …)`.
3. A per-request access decision, so the host's `IWopiPermissionProvider` result drives `QueryAccess` and write checks instead of stored grants. **beta.2:** `CellBridgeActor.AccessLimit` plus `ICellBridgeAuthorizationPolicy`; both are synchronous and expect the host to resolve permissions before the transaction, which matches the conclusion below.
4. A publication hook (revision published → bytes) instead of comparing `ContentVersion` after each call, so the host writes the file through its own storage provider exactly once per save. **beta.2:** `ExternalRevisionPublisher` over `IExternalRevisionDestination.CompareExchangeAsync(request, verifiedContent)`. Its contract asks for more than `IWopiWritableFile.OpenWriteAsync` offers — an atomic compare-exchange on a revision token and a durable, deduplicating receipt — so this is the concrete gap in WopiHost's storage contract the exercise surfaces.
5. Delete/evict on the state store, so idle documents can leave memory the way `CobaltProcessor` evicts sessions. **beta.2:** `IDocumentLifecycleStore.TryDeleteAsync` covers delete; idle eviction remains the adapter's.

What the exercise said about WopiHost's own contracts: `ICobaltProcessor` is the right seam (no `WopiHost.Core` change was needed) but drops the request Content-Type, which an MTOM body would need; `IWopiWritableFile.OpenWriteAsync` has no version precondition, so a host cannot ask for "write only if still at version N" the way cellbridge's publish does; and `IWopiPermissionProvider` is async and principal-based where cellbridge needs a synchronous answer inside a transaction, so an adapter has to resolve permissions up front and hand the result in.

## Distribution

cellbridge publishes its nine libraries to NuGet.org as `0.1.0-beta.*` prereleases (`CellBridge.AspNetCore`, `CellBridge.FssHttp`, `CellBridge.FssHttpB`, `CellBridge.Storage`, `CellBridge.Storage.Abstractions`, `CellBridge.Storage.InMemory`, `CellBridge.Storage.FileSystem`, `CellBridge.Storage.PostgreSql`, `CellBridge.Storage.Conformance`) — the channel WopiHost's own packages use, so `Directory.Packages.props` and Dependabot track them. A container image would suit cellbridge's *standalone* server, but WopiHost needs the engine in-process behind `ICobaltProcessor`, so NuGet is the channel that matters here. Only cellbridge's demo host and tools stay source-only; the AppHost lane pins a commit for those.

## License

[MIT](../../LICENSE.txt). cellbridge is MIT-licensed by Patrick Matthiesen; its vendored Microsoft protocol test code retains its own notice.
