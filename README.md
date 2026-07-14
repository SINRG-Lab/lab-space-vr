# Lab Space VR

Unity/Meta Quest VR project focused on a guided split tube furnace workflow. The current main demo scene is `Assets/Scenes/OTF.unity`.

## Demo Focus

The priority is the furnace procedure itself: interacting with the split tube furnace, preparing and feeding the substrate into the quartz tube, setting gas flow and heating zones, running the nanowire growth process, and withdrawing the substrate cleanly.

Hologram/WebRTC streaming is intentionally deferred until the furnace flow is stable.

## Furnace Demo Polish Tracker

Status legend: `Done`, `In Progress`, `Next`, `Pending`.

| Phase | Status | Scope | Acceptance criteria |
| --- | --- | --- | --- |
| 0. OTF as primary demo | Done | Make `OTF` the active build scene and prevent hologram auto-start. | Build settings launch `OTF`; hologram sender waits for manual start. |
| 1. Procedure manager foundation | In Progress | Add one central state machine for the furnace procedure. | Scene can show current step, advance only when required gates are marked complete, and reset to the beginning. |
| 2. Load and feed substrate | Next | Polish substrate plate snapping, rod connection, and quartz tube feeding. | User sees ghost target/highlight, substrate snaps reliably, and the procedure advances only after the substrate is correctly positioned. |
| 3. Power and furnace controls | Pending | Gate main power, lid/handle state, heating ready/off controls, and UI visibility. | Controls unlock in sequence; irrelevant controls are hidden or visually demoted until they are needed. |
| 4. Gas flow | Pending | Make the valve readout deterministic and use a minimum gas-flow gate. | Readout is stable, displays useful units, and the procedure blocks heating until flow is ready. |
| 5. Temperature ramp and soak | Pending | Tighten three-zone setpoint entry, ramp animation, material feedback, and soak completion. | Zones show target/current values clearly; heating reaches target predictably under sim speed; completion is visible. |
| 6. Growth start and visualization | Pending | Start nanowire growth only after furnace prerequisites are satisfied. | Growth controls are unavailable before prerequisites; growth starts with clear feedback and sane defaults. |
| 7. Cooldown and withdraw | Pending | Add cooldown/withdraw steps and reset behavior. | User can complete the procedure by cooling down and withdrawing the substrate without physics glitches. |
| 8. Demo polish pass | Pending | Add audio, haptics, labels, reset button, lighting/performance pass, and final rehearsal checklist. | A first-time viewer can follow the demo without verbal rescue; Quest performance remains stable. |

## Target Procedure

1. Power on the furnace.
2. Prepare the substrate plate and catalyst settings.
3. Load the substrate onto the feed mechanism.
4. Align and feed the substrate into the quartz tube.
5. Set gas flow.
6. Set the three furnace temperature zones.
7. Heat to target and complete the soak.
8. Start nanowire growth.
9. Cool down.
10. Withdraw the substrate and reset for the next run.

## Current Implementation Notes

- `ProjectSettings/EditorBuildSettings.asset` now enables `Assets/Scenes/OTF.unity` as the primary scene.
- `Assets/Scenes/OTF.unity` keeps `HologramSender.waitForManualStart` enabled so hologram work does not interrupt the furnace flow.
- The furnace flow currently uses several focused scripts, including `SnapOnRelease`, `AutoConnectEnd`, `AngleTrigger`, `RotationToGasFlow`, `IncreaseTemperature`, `Setting_Parameter`, and `GrowthRate`.
- New work should route procedural progress through `FurnaceProcedureManager` instead of adding more one-off scene-only checks.

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
