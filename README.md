# Lab Space VR

Unity/Meta Quest VR project focused on a guided split tube furnace workflow. The current main demo scene is `Assets/Scenes/OTF.unity`.

## Demo Focus

The priority is the furnace procedure itself: interacting with the split tube furnace, preparing and feeding the substrate into the quartz tube, setting gas flow and heating zones, running the nanowire growth process, and withdrawing the substrate cleanly.

Hologram/WebRTC streaming is intentionally deferred until the furnace flow is stable.

## Furnace Demo Polish Tracker

Status legend: `Done`, `In Progress`, `Next`, `Pending`.

| Phase | Status | Scope | Acceptance criteria |
| --- | --- | --- | --- |
| 0. OTF as primary demo | Done | Make `OTF` the active build scene and keep deferred hologram systems inactive at startup. | Build settings launch `OTF`; hologram capture cameras, composition, output, and streaming stay inactive. |
| 1. Configurable procedure manager | Done | Add a central manager with reorderable procedure steps and stable completion gates. | Flow order, titles, instructions, required gates, and per-step events can be edited on the manager without code changes. |
| 2. Wire first OTF gates and UI | Done | Add/assign procedure UI and connect main power plus first substrate snap gate. | User sees current instruction, power-on advances the flow, and substrate placement advances only after a valid snap. |
| 3. Load and feed substrate | Done | Polish substrate plate snapping, rod connection, and quartz tube feeding. | User sees ghost target/highlight, substrate snaps reliably, and the procedure advances only after the substrate is correctly positioned. |
| 4. Power and furnace controls | Done | Gate main power, lid/handle state, heating ready/off controls, and UI visibility. | Controls unlock in sequence; irrelevant controls are hidden or visually demoted until they are needed. |
| 5. Gas flow | In Progress | Make the valve readout deterministic and use a minimum gas-flow gate. | Readout is stable, displays useful units, and the procedure blocks heating until flow is ready. |
| 6. Temperature ramp and soak | Pending | Tighten three-zone setpoint entry, ramp animation, material feedback, and soak completion. | Zones show target/current values clearly; heating reaches target predictably under sim speed; completion is visible. |
| 7. Growth start and visualization | Pending | Start nanowire growth only after furnace prerequisites are satisfied. | Growth controls are unavailable before prerequisites; growth starts with clear feedback and sane defaults. |
| 8. Cooldown and withdraw | Pending | Add cooldown/withdraw steps and reset behavior. | User can complete the procedure by cooling down and withdrawing the substrate without physics glitches. |
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

## Target Procedure

This is the starter flow, not a fixed order. Edit the `steps` list on `FurnaceProcedureManager` to reorder, split, combine, or remove steps as the real furnace procedure becomes clearer.

1. Power on the furnace.
2. Prepare the substrate plate and catalyst settings.
3. Load the substrate onto the feed mechanism.
4. Connect the feed rod to the substrate holder.
5. Align and feed the substrate into the quartz tube.
6. Close the furnace lid.
7. Set gas flow.
8. Set the three furnace temperature zones.
9. Heat to target and complete the soak.
10. Start nanowire growth.
11. Cool down.
12. Withdraw the substrate and reset for the next run.

## Current Implementation Notes

- `ProjectSettings/EditorBuildSettings.asset` now enables `Assets/Scenes/OTF.unity` as the primary scene.
- `Assets/Scenes/OTF.unity` starts both the hologram `Cameras` group and the `HologramComposer` group inactive, preventing capture cameras, render-texture allocation, composition, and WebRTC work during the furnace demo.
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
- `FeedRailController` drives the rod and plate from one smoothed rail distance, settles at the exact endpoint, marks `SubstrateFedIntoTube`, disconnects the rod, and locks the delivered plate in place.
- The procedure panel now normalizes its progress range, displays the current step number from the reorderable `steps` list, and changes to the accepted completion color when the run finishes.
- Substrate snapping, rod connection, and feed-rail guidance are enabled by their stable procedure gates rather than fixed step numbers, so the same interactions follow any reordered procedure.
- `FurnaceInteractionFeedback` centralizes target and confirmation audio for the hand-tracking flow; visual guides provide the corresponding spatial feedback without requiring controllers.
- Each procedure step exposes optional `prerequisiteGates`, `activeObjects`, `activeBehaviours`, and `activeSelectables` lists. Prerequisites keep controls unavailable until stable safety conditions are true without coupling that behavior to a fixed step index.
- Main power now reports both on and off states to the procedure manager. The six temperature buttons are interactable only during `Set Temperature Zones`, and the physical heating-ready poke control is enabled only during `Heat and Soak` while power remains on.
- `FurnaceLidState` watches the existing hand-driven lid hinge and publishes the flow-independent `FurnaceClosed` gate. OTF uses it for a reorderable `Close Furnace` step and as a heating prerequisite, so reopening the lid disables the heating-ready poke control.
- `RotationToGasFlow` measures quaternion distance from the valve's configured minimum pose, quantizes the readout to `50 sccm`, and publishes `GasFlowReady` at `1000 sccm` with `100 sccm` release hysteresis. The readout and particle visualization use the same normalized source.
- `Heat and Soak` requires both `FurnaceClosed` and `GasFlowReady`, so either reopening the lid or reducing flow below the safe band disables its physical poke control.
- `FurnaceStepIndicator` renders one lightweight pulsing world-space arrow for the current step. Its target and offset live on each reorderable procedure entry; the first six implemented interactions now include the gas-flow valve.

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
