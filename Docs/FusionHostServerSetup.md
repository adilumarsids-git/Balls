# Fusion Host/Server Migration & Scene Wiring Guide

This project has been migrated from **Fusion Shared Mode** to **Host/Server** topology.

- Host uses `GameMode.Host`
- Joiners use `GameMode.Client`
- Authoritative spawning/game logic run on server (`Runner.IsServer` + `Object.HasStateAuthority`)

---

## 1) What changed in code

### Session startup
- `FusionLauncher.Host(...)` now starts with `GameMode.Host`.
- `FusionLauncher.Join(...)` now starts with `GameMode.Client`.
- Public lobby is `SessionLobby.ClientServer`.

### Player spawn/despawn authority
- Player spawning now happens **only on server** from `OnPlayerJoined`.
- Scene-load backfill spawns are server-only (`OnSceneLoadDone`).
- Player despawn on leave is server-only (`OnPlayerLeft`).
- `PlayerSpawner` tracks spawned players in a `Dictionary<PlayerRef, NetworkObject>` so each `PlayerRef` spawns once.

### State authority ownership
- Removed client-side `RequestStateAuthority()` behavior from `NetworkPlayerController`.
- Movement simulation remains in `FixedUpdateNetwork()` and runs only on `Object.HasStateAuthority` (server in Host mode).

### Authoritative gameplay flow
- `NetworkGameFlowManager` now uses server/state-authority checks (instead of Shared-master checks).
- Ring-out and consumable reports route to server-only flow methods.
- Broadcast RPCs are sent from `RpcSources.StateAuthority`.

---

## 2) Required scene wiring checklist

Follow this for **Bootstrap scene** and each **gameplay map scene**.

## Bootstrap scene (runner host object)

On your bootstrap network object (the one that lives across scenes), ensure these components exist:

1. `NetworkRunner`
2. `FusionLauncher`
3. `FusionInputProvider`
4. `PlayerSpawner`
5. `NetworkSceneManagerDefault` (auto-added by launcher if missing, but recommended to add explicitly)

Then verify:

- `FusionLauncher` references are configured:
  - `menuSceneBuildIndex`
  - map build indexes used by UI host flow
- `PlayerSpawner.playerPrefab` points to your player `NetworkObject` prefab.

## Lobby scene/UI

On your lobby UI object:

- `LobbyMenuUI.launcher` should reference the scene/bootstrap `FusionLauncher` (or allow runtime `FindObjectOfType`).
- Host button should call `launcher.Host(...)` (already done by script).
- Join button should call `launcher.Join(...)` (already done by script).

## Gameplay map scenes

Each map scene must include:

1. A GameObject named exactly **`SpawnPoints`** with child transforms for player spawn locations.
   - Parent transform is ignored.
   - Child transforms are used as spawn slots.

2. A scene `NetworkObject` containing `NetworkGameFlowManager`.
   - Set `requiredPlayers`, countdown, and menu return index as desired.
   - Assign `consumablePrefab` if consumables are used.

3. Ring-out collider zones with `RingOutZone` component.

4. (Optional) A GameObject named **`ConsumableSpawnPoints`** with child transforms.
   - Used by `NetworkConsumableSpawner`/flow for consumable respawns.

## Player prefab wiring

Player prefab should contain:

- `NetworkObject`
- `NetworkPlayerController`
- `NetworkRigidbody3D`
- `Rigidbody` (compatible with Fusion addon setup)
- `PlayerTag`
- `PlayerStats` (if used by movement/buffs)
- Any appearance components (`NetworkPlayerAppearance`) used by winner/leaderboard flow

Also ensure the prefab is registered in Fusion network prefabs (Project config) and assigned to `PlayerSpawner.playerPrefab`.

---


## Host-leave continuity (Host Migration)

To keep the match alive when the current host exits, enable Fusion host migration in your project config:

1. Open the Fusion `NetworkProjectConfig` asset in Unity.
2. Enable **Host Migration**.
3. Set a sensible snapshot update delay (e.g. 1-2 seconds).
4. Keep `FusionLauncher.enableHostMigration` enabled on the bootstrap runner object.

Without Host Migration enabled in config/cloud, clients will still disconnect when the host exits.

Reconnect fallback: if a host-migration token is not received in time, `FusionLauncher` now attempts automatic client reconnects to the same session before sending players back to menu.

---


## Scene audit notes (current project)

I checked the project scenes and found these important items:

- `00_Bootstrap` already contains `NetworkRunner`, `FusionLauncher`, `FusionInputProvider`, `PlayerSpawner`, and `NetworkSceneManagerDefault` on `NetworkBootstrap`.
- `10_Map_Island_A` and `11_Map_Island_B` both contain a correctly named `SpawnPoints` object.
- For `NetworkGameFlowManager` and `NetworkMatchManager` scene `NetworkObject` components, uncheck **Is Master Client Object** for Host/Server topology.
- Both maps contain many misspelled `ConsumeableSpawnPoints` objects and one correctly spelled `ConsumableSpawnPoints` object.
  - Current code only uses `ConsumableSpawnPoints` (correct spelling), so keep at least one correctly named object.
  - You can safely remove/rename the misspelled duplicates to avoid confusion.
- `NetworkProjectConfig.fusion` must have:
  - For WebGL builds hosting/joining in Host/Server, `AllowClientServerModesInWebGL` must be `true` (otherwise host start fails with `IncompatibleConfiguration`).
  - `Simulation.InputDataWordCount` set for your input struct (this repo now uses `3` for `Vector2 + Boost`).
  - `HostMigration.EnableAutoUpdate = true` and a low `UpdateDelay` (this repo now uses `2`).

---

## 3) Validation steps in Editor

Run this quick end-to-end check with 2+ clients:

1. Start one instance as host.
2. Start one or more clients and join the same room.
3. Confirm each joining player spawns exactly once.
4. Confirm host and clients can move their own player (input still works).
5. Confirm ring-out only resolves once and winner flow transitions correctly.
6. Confirm consumable pickup/respawn still works and remains authoritative.
7. Disconnect a client and confirm their player despawns.

---

## 4) Common pitfalls

- Missing or misnamed `SpawnPoints` object in map scene.
- `PlayerSpawner.playerPrefab` not assigned.
- Gameplay scene missing `NetworkGameFlowManager` network object.
- Player prefab missing required Fusion/physics components.
- Build index mismatch between UI map selection and actual build settings.

---

## 5) Optional hardening ideas

- Use per-player metadata (name/NFT) from a replicated source when setting player names on server.
- Add guard logs for duplicate spawn attempts with existing object IDs.
- Add PlayMode test harness for host + one client spawn/movement smoke test.
