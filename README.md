# Lab Space VR

Unity/Meta Quest VR project focused on a guided split tube furnace workflow. The current main demo scene is `Assets/Scenes/OTF.unity`.

## Demo Focus

The priority is the furnace procedure itself: interacting with the split tube furnace, preparing and feeding the substrate into the quartz tube, setting gas flow and heating zones, running the nanowire growth process, and withdrawing the substrate cleanly.

The furnace procedure remains the source of truth. The hologram mirrors the current procedure step instead of running as a separate nanowire-only experience.

## Furnace Demo Polish Tracker

Status legend: `Done`, `In Progress`, `Next`, `Pending`.

| Phase | Status | Scope | Acceptance criteria |
| --- | --- | --- | --- |
| 0. OTF as primary demo | Done | Make `OTF` the active build scene and keep archived hologram experiments out of the runtime path. | Build settings launch `OTF`; only the maintained procedure-driven hologram path can activate. |
| 1. Configurable procedure manager | Done | Add a central manager with reorderable procedure steps and stable completion gates. | Flow order, titles, instructions, required gates, and per-step events can be edited on the manager without code changes. |
| 2. Wire first OTF gates and UI | Done | Add/assign procedure UI and connect main power plus first substrate snap gate. | User sees current instruction, power-on advances the flow, and substrate placement advances only after a valid snap. |
| 3. Load and feed substrate | Done | Polish substrate plate snapping, rod connection, and quartz tube feeding. | User sees ghost target/highlight, substrate snaps reliably, and the procedure advances only after the substrate is correctly positioned. |
| 4. Power and furnace controls | Done | Gate main power, lid/handle state, heating ready/off controls, and UI visibility. | Controls unlock in sequence; irrelevant controls are hidden or visually demoted until they are needed. |
| 5. Gas flow | Done | Make the valve readout deterministic and use a minimum gas-flow gate. | Readout is stable, displays useful units, and the procedure blocks heating until flow is ready. |
| 6. Temperature ramp and soak | Done | Tighten three-zone setpoint entry, ramp animation, material feedback, and soak completion. | Zones show target/current values clearly; heating reaches target predictably under sim speed; completion is visible. |
| 7. Growth start and visualization | Done | Confirm growth parameters before furnace operation, then start nanowire growth only after furnace prerequisites are satisfied. | Parameters are validated and locked at the start of the procedure; growth starts later with clear feedback and sane defaults. |
| 8. Cooldown and withdraw | Done | Add cooldown, furnace reopening, withdrawal, and reset behavior. | User can complete the procedure by cooling down and withdrawing the substrate without physics glitches. |
| 9. Demo polish pass | In Progress | Add audio, hand-tracking feedback, labels, reset button, lighting/performance pass, and final rehearsal checklist. | A first-time viewer can follow the demo without verbal rescue; Quest performance remains stable. |

## Visual Polish Tracker

Each stage is validated visually and on Quest before the next stage begins.

| Stage | Status | Scope | Acceptance criteria |
| --- | --- | --- | --- |
| 1. Lighting baseline | Done | Activate the laboratory, localize the ceiling lights, and establish a user-aligned furnace key light. | The furnace is clearly modeled by light, moving parts remain readable, and the full laboratory remains stable on Quest. |
| 2. Material pass | Done | Differentiate painted metal, stainless steel, quartz, ceramic, controls, and labels. | Major surfaces read as distinct physical materials without relying on excessive texture resolution. |
| 3. Color finishing | Done | Add VR-safe tonemapping, color adjustment, and restrained emissive effects. | Contrast and temperature are coherent without distracting bloom or reduced readability. |
| 4. Interaction presentation | Done | Unify current-step guides, status colors, sound, hand-tracking feedback, and control visibility. | The procedure is understandable without visual clutter or verbal rescue. |
| 5. Quest finalization | Pending | After the remaining furnace flow is implemented, profile the completed scene and tune resolution, LOD, batching, and remaining hotspots. | The final demo sustains its target frame rate through the full furnace procedure. |

## Hologram Tracker

The hologram follows the same reorderable steps as the furnace guide. Each step changes the shared camera focus and zoom while retaining the furnace apparatus context without the surrounding laboratory background.

| Stage | Status | Scope | Acceptance criteria |
| --- | --- | --- | --- |
| H1.1 Apparatus isolation | In validation | Render the furnace, quartz tube, substrate, feed rod, gas/growth effects, relevant controls, and procedure indicator against black. | The hologram excludes the table, floor, walls, and unrelated laboratory equipment without changing the normal Quest view. |
| H1.2 Context camera focus | In validation | Preserve the established four-camera poses while moving their shared rig to the current procedure target and scaling the original offsets for zoom. | Four consistently oriented views follow the same target without changing the authored compositor alignment. |
| H1.3 Compositor validation | Done | Validate the existing `CompositerCamera` diamond output locally without WebRTC. | Orientation, flips, framing, and scale can be checked in the Editor without a receiver. |
| H1.4 Lifecycle and performance | In validation | Allocate and render only while the procedure hologram component is enabled. | Disabling procedure hologram mode releases its render textures; the active preview stays within the Quest frame budget. |
| H1.5 Procedure focus | In validation | Drive hologram content and camera presentation entirely from settings stored on each procedure step. | All 13 steps retain their own target, masking, distance, FOV, and spacing behavior when reordered. |
| H1.6 Runtime stream modes | In validation | Switch the existing four-camera stream between procedure focus, operator view, hand follow, and head follow. | The active mode can be changed from the persistent Quest panel or Editor driver without changing the authored camera layout. |
| H2 Streaming and receiver | Pending | Harden signaling, WebRTC sender, receiver, discovery, reconnect, and fullscreen calibration. | Quest streams the calibrated output to the physical hologram display reliably. |

The OTF compositor uses the existing `CompositerCamera`, quad, material, and shader path. `Content Scale` remains an independent Inspector setting (currently targeted at `0.75`), while camera framing controls distance and surrounding context.
The four source cameras retain their existing front/right/back/left assignments, local positions, and local rotations. Runtime framing translates their shared rig to the current focus and scales the original camera offsets for zoom; it does not regenerate camera angles or remap the compositor views.
The composer applies a `0.8` global camera-distance multiplier. Every procedure step exposes `Hologram Focus Targets`, `Use Capture Layer Bounds`, `Hide Apparatus`, `Context Radius`, `Distance Multiplier`, `Field Of View`, `Camera Y Offset`, `Camera Rotation Offset`, `Center Cameras On Focus`, and `Use Authored Camera Spacing`. `HologramProcedureFocus` passes these values directly to the composer without checking a step ID, so each view remains independently tunable and follows its step when reordered. Position and rotation changes pivot the shared four-camera rig around the active focus; optional per-camera centering applies the minimum additional aim correction while retaining each camera's position and roll. The active step's settings are checked every `0.1 sec` and reapplied when changed, allowing live Play-mode tuning; Play-mode edits must still be copied to Edit mode to persist.
The persistent hologram selector provides four modes: `Procedure` uses the current step settings; `Operator` keeps the authored camera ring fixed and aims it between the operator's head and tracked hands; `Hands` moves the ring to the tracked-hand midpoint after a `0.1 m` movement threshold; and `Head` applies CenterEye world translation plus yaw to the complete ring while ignoring head pitch and roll. Operator-tracking modes include the furnace apparatus and available virtual hand renderers. The selector itself is excluded from the hologram capture.
The authoritative receiver and signaling projects are `/Users/barath/BarathMac/HologramReceiver` and `/Users/barath/BarathMac/HologramSignaling`. OTF starts its existing `HologramSender` with the first procedure step; connect the receiver to the signaling server before starting OTF because the current server does not replay an offer that was sent before the receiver registered.

## Target Procedure

This is the starter flow, not a fixed order. Edit the `steps` list on `FurnaceProcedureManager` to reorder, split, combine, or remove steps as the real furnace procedure becomes clearer.

1. Set and confirm the nanowire radius, target height, and catalyst count.
2. Power on the furnace.
3. Load the substrate onto the feed mechanism.
4. Connect the feed rod to the substrate holder.
5. Align and feed the substrate into the quartz tube.
6. Close the furnace lid.
7. Set gas flow.
8. Set the three furnace temperature zones.
9. Heat to target and begin the soak.
10. Grow the nanowires automatically while the soak continues.
11. Cool down.
12. Open the furnace.
13. Reconnect the feed rod, withdraw the substrate, and reset for the next run.

## Current Implementation Notes

- `ProjectSettings/EditorBuildSettings.asset` now enables `Assets/Scenes/OTF.unity` as the primary scene.
- `HologramProcedureFocus` starts the maintained composer/sender path and follows `FurnaceProcedureManager.StepEntered`. Each step owns `hologramFocusTargets` and `hologramContextRadius`, so focus and zoom remain attached when steps are reordered.
- Procedure focus temporarily places only the OTF apparatus, feed rod, substrate, current focus hierarchy, and step indicator on the capture layer while each hologram camera renders. Original layers are restored immediately afterward so normal Quest rendering, collisions, and hand interactions are unchanged.
- Nanowire spawning excludes the inactive scene template from progress tracking; the configured catalyst count now represents active growth visuals, allowing progress to reach `100%` instead of stopping at `96%` with 24 catalysts.
- The laboratory environment is active and has been validated without reintroducing the previous startup lag.
- OTF now uses six shadowless point lights for room illumination, removing the previous over-capacity punctual-light shadow workload.
- The remaining ceiling-light ranges are `5.5 m`, preserving floor coverage while preventing every light from flooding the entire laboratory.
- The user-aligned `Furnace Key Light` at `(-0.8, 3.224, 2.2)` is a downward-facing spotlight with hard, medium-tier shadows.
- `PullingRod.mat` now uses a metallic, moderately smooth response so the feed rod reads as brushed stainless steel under the accepted lighting setup.
- `Substrate.mat` now uses a dark blue-gray, smooth silicon-wafer response instead of the previous bright prototype blue.
- `Plate.mat` now uses a cool off-white, lightly smooth ceramic response that stays distinct from the dark substrate.
- The OTF scene now uses explicit renderer overrides for reusable furnace materials covering the satin painted shell, matte ceramic interior, dark structural hardware, control panel, screen glass, and powered-on indicator; the original FBX embedded material references remain intact.
- The user added `FurnaceBorder` and manually realigned the `Furnace_Base` and `Upper_1U` material slots. Treat these scene-level assignments as the accepted Stage 2 baseline and do not replace them with automatic FBX importer remaps.
- Shared flange/support steel is less mirror-like, quartz has a subtle cool tint and visible edge response, and laboratory walls use a lower-gloss neutral finish.
- Control-panel screens, markers, buttons, knobs, and wire covers now use purpose-appropriate surfaces while preserving the existing interaction and label hierarchy.
- `OTF Color Volume` applies a dedicated low-cost profile to the active `CenterEyeAnchor` only: ACES tonemapping, `+6` contrast, `-6` saturation, and restrained bloom at `1.25` threshold and `0.06` intensity.
- Post-processing remains disabled on the inactive hologram/compositor cameras, and the OTF profile avoids depth of field, vignette, chromatic aberration, film grain, and high-quality bloom filtering.
- The furnace flow currently uses several focused scripts, including `SnapOnRelease`, `AutoConnectEnd`, `AngleTrigger`, `RotationToGasFlow`, `IncreaseTemperature`, `Setting_Parameter`, and `GrowthRate`.
- New work should route procedural progress through `FurnaceProcedureManager` instead of adding more one-off scene-only checks.
- `FurnaceProcedureManager` separates flow order from completion state: procedure steps are a serialized list, while scene interactions mark stable gates such as `PowerOn`, `SubstrateLoaded`, `GasFlowReady`, and `HeatSoakComplete`.
- `SnapOnRelease` now creates a placement guide while the plate is held, accepts near-boundary releases, eases to one exact target pose, applies its rail constraints immediately, and marks `SubstrateLoaded` only after placement completes.
- `AutoConnectEnd` now matches named plug/socket endpoints, previews the connection while the rod is held, and asks `FeedRailController` for a rail-aligned connection pose so model endpoint rotations cannot leave the rod vertical.
- `FeedRailController` drives the rod and plate from one smoothed rail distance, interpolates the plate toward the explicit `Feed Rail End` center target, enforces that exact final pose before detaching the rod, and locks the delivered plate in place. During withdrawal it accepts a second rod connection and reverses the same constrained rail to the original plate pose.
- The procedure panel now normalizes its progress range, displays the current step number from the reorderable `steps` list, and changes to the accepted completion color when the run finishes.
- Substrate snapping, rod connection, and feed-rail guidance are enabled by their stable procedure gates rather than fixed step numbers, so the same interactions follow any reordered procedure.
- `FurnaceInteractionFeedback` centralizes target and confirmation audio for the hand-tracking flow; visual guides provide the corresponding spatial feedback without requiring controllers.
- Each procedure step exposes optional `prerequisiteGates`, `activeObjects`, `activeBehaviours`, `disabledBehaviours`, and `activeSelectables` lists. Prerequisites keep controls unavailable until stable safety conditions are true without coupling that behavior to a fixed step index.
- Main power now reports both on and off states to the procedure manager. The six temperature buttons are interactable only during `Set Temperature Zones`, and the physical heating-ready poke control is enabled only during `Heat and Soak` while power remains on.
- `FurnaceLidState` watches the existing hand-driven lid hinge and independently publishes `FurnaceClosed` at `-90 degrees` and `FurnaceOpen` at `-40 degrees`. OTF uses these for reorderable close/open steps and heating/withdrawal prerequisites.
- `RotationToGasFlow` measures quaternion distance from the valve's configured minimum pose, quantizes the readout to `50 sccm`, and publishes `GasFlowReady` at `1000 sccm` with `100 sccm` release hysteresis. The readout and particle visualization use the same normalized source.
- Gas-flow particles are active in OTF and were validated with the physical valve interaction.
- The three temperature controls use `50 C` steps for demo-speed entry and publish `TemperatureZonesSet` only after every zone reaches the configurable `500 C` minimum.
- `IncreaseTemperature` now owns one deterministic three-zone ramp and timed soak. It follows `GlobalSimSpeed`, pauses when power, lid, gas flow, or setpoint safety becomes invalid, and publishes `HeatSoakComplete` when all three zones reach their setpoints so growth can run while the soak continues.
- The existing `HEATING OFF` poke control is enabled only during `Cool Down`. It starts a deterministic material/readout cooldown and publishes `CooldownComplete` when every zone reaches the `50 C` safe-withdrawal threshold.
- `Interior_Split` and `Interior_Split_Lower` now heat independently by zone. Every ceramic material slot preserves its cold appearance, shifts through the configured temperature gradient, and begins emissive glow above `200 C`.
- `Heat and Soak` requires `PowerOn`, `FurnaceClosed`, `GasFlowReady`, and `TemperatureZonesSet`, so invalidating any safety condition disables its physical poke control and pauses an active heat cycle.
- The parameter panel is now the first procedure step. Its primary button reads `Confirm`, validates and snapshots radius, target height, and catalyst count, then hides the panel before furnace operation begins. The physical power control remains locked until confirmation.
- `GrowthManager` automatically starts nanowire growth when all three temperature zones first reach their configured setpoints. The soak continues concurrently, and the procedure remains on `Nanowire Growth` until every wire reaches its requested height or becomes collision-limited.
- The procedure progress bar displays live nanowire completion during `Nanowire Growth`. The parameter panel and action button stay hidden for that automatic step, and the indicator arrow remains hidden until `Cool Down` requires the heating-off control.
- The Quest-oriented default is `24` growth visuals with a configurable cap of `32`, replacing the previous startup value of `100` physics-bearing nanowire objects.
- Growth uses the existing physical rate model with a demo acceleration of `100,000,000x` and follows the global procedure simulation multiplier.
- The parameter panel shows the accelerated VR `Simulation` time. It updates from radius and target height; catalyst count does not change it because the current visuals grow in parallel.
- The Configure Growth dropdowns now use separate toggle groups. Required Height options no longer register with the Radius dropdown, so all three parameter selectors update independently.
- The legacy `Enable Twin` button is disabled in `OTF`; the procedure-owned button handles `Confirm` during parameter setup, and growth starts automatically at target temperature.
- `LocomotionCollisionSetup` adds the lab's `Walkable` layer to Meta's character collision mask before locomotion starts, so the rig grounds against the floor instead of vertically correcting against the table.
- The current-step indicator now points to the middle setpoint display during temperature setup and to the physical `HEATING READY` control during ramp start.
- `FurnaceStepIndicator` renders one lightweight pulsing world-space arrow for the current step. Its target and offset live on each reorderable procedure entry; the first six implemented interactions now include the gas-flow valve.
- `FurnaceDevHarness` provides an Editor-only desktop driver for the complete configurable procedure. It changes the actual furnace component state for implemented phases and disables itself in Quest builds, so it does not replace or alter hand tracking.
- The Development Driver also exposes `Procedure`, `Operator`, `Hands`, and `Head` hologram buttons. In Editor, inactive OVR hand tracking falls back to the hand-anchor transforms so the tracking modes can be checked without a Quest connection.
- When withdrawal completes, the procedure panel instructs the user to turn the physical main power switch off and the world-space arrow points to that switch.
- `FurnaceStationReset` handles the resulting end-of-run reset in one place. It restores the substrate, feed rod, lid, gas flow, temperatures, growth state, parameter confirmation, power, and procedure gates for the next run; turning power off before procedure completion does not trigger this full reset.
- Substrate snap, rod connection, reverse withdrawal, and station reset now clear Rigidbody momentum only while bodies are dynamic. Kinematic movement no longer generates Unity velocity-assignment warnings during the Phase 8 flow.

## Editor Testing Without Quest

Open `Assets/Scenes/OTF.unity` and enter Play mode. The **Furnace Development Driver** appears in the Game view and follows the current serialized `steps` order, so reordering the procedure does not require changing the harness.

- `Simulate Current` runs the current interaction with its normal motion or timing.
- `Complete Instant` moves the real component to its completed state immediately.
- For the Development Driver only, `Feed Substrate` and `Withdraw Substrate` translate the plate directly between its exact endpoints; the Quest hand-driven forward and reverse rail behavior is unchanged.
- `Auto Run` resets and simulates the full procedure; `Instant Run` prepares every implemented state immediately.
- The Development Driver `Reset` action uses the same `FurnaceStationReset` path as the completed Quest procedure.
- Select any listed step and use `Jump To Selected` to rebuild all earlier state and stop there. A step whose stable gate is already satisfied may be skipped automatically by the procedure manager.
- The safety-fault buttons toggle main power, lid state, and gas flow so heat pause/resume behavior can be checked without a headset.
- The gate list shows the same stable state used by the Quest procedure. A timeout indicates a missing reference or a component that did not reach its expected state.

Keyboard shortcuts while the Game view is focused:

| Shortcut | Action |
| --- | --- |
| `` ` `` | Show or hide the development panel |
| `Shift+R` | Reset the furnace flow |
| `Shift+Enter` | Simulate the current step |
| `Shift+N` | Complete the current step instantly |
| `Shift+J` | Jump to the selected step |
| `Shift+A` | Start a full auto-run |
| `Shift+P` | Pause or resume auto-run dispatch |
| `Shift+[` / `Shift+]` | Decrease or increase simulation speed |
| `Shift+1` / `Shift+2` / `Shift+3` | Toggle power, lid, or gas fault |

Phase 8 is implemented end to end and can be exercised in the Development Driver. Final Quest checks for rod reconnection reach, reverse-pull comfort, occlusion, depth perception, and sustained device performance remain tracked under Phase 9 and Visual Stage 5.

## Split Tube Furnace Model Previews

### Front View

![Front view](Images/FrontView.jpeg)

Three-zone heater liners and the quartz tube passing through the furnace body. The control panel with zone selectors and indicators is visible on the lower front panel. Left and right ends show the flange hardware and tube supports.

### Isometric View

![Isometric view](Images/IsoView.jpeg)

Angled perspective showing the overall form factor, the split-lid cut-outs, and the tube path.

### Side View

![Side view](Images/SideView.jpeg)

Side profile with the lid raised showing the hinge motion and feedthrough cut-out alignment.

## Nanowire Growth Preview

![Nanowire growth preview](https://github.com/user-attachments/assets/74e0b3a2-e984-4341-a481-484ff208a710)

Initial parameters used: radius, temperature, and required nanowire height. The visualization scale uses meters for easier viewing in VR.
