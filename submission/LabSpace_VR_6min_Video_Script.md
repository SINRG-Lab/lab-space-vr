# LabSpace VR — 6:30 competition video script

Target: 6 minutes 20–40 seconds, 1920×1080, MP4, 30 or 60 fps. Record clean Quest footage first and add the narration afterward. Use captions throughout.

Project framing: Theme 3 — Advanced Learning in Educational or Classroom Environments.

## Three-person speaker plan

| Person | Video role | Approximate speaking time |
| --- | --- | ---: |
| Professor | Faculty lead: academic need, educational value, and closing | 1:25 |
| Barath Balamurugan | Main developer: product walkthrough and implementation | 4:10 |
| Yilin | Project contributor and learner demonstrator: guidance, usability, and shared-view experience | 0:55 |

Use lower-thirds the first time each person appears:

- Professor **[full name]** — Faculty Lead
- Barath Balamurugan — Main Developer
- Yilin **[last name]** — Project Contributor

The professor should appear on camera for the opening and closing. Barath should appear briefly before the technical walkthrough, then narrate over the screen capture. Yilin should be visibly using the headset during the preparation/process shots and speak on camera or in voice-over during the state-adaptive guidance section.

## 0:00–0:22 — Hook

Visual: Start on the finished furnace in the lab. Cut quickly to a hand turning a control, the heated interior, and the nanowire growth view. On-screen title: “LabSpace VR — Practice nanowire growth before touching the furnace.”

Speaker — Professor, on camera:

“Growing nanowires in a split-tube furnace is not learned by memorizing a list. A learner must coordinate substrate handling, gas flow, three heating zones, timing, growth, cooldown, and safe withdrawal. LabSpace VR turns that complete process into guided, hands-on rehearsal before the learner touches the real instrument.”

## 0:22–0:58 — The learning problem

Visual: Show the real-scale virtual furnace from front and side. Briefly overlay three words: “Sequence. Safety. Process response.”

Speaker — Professor, continuing over furnace B-roll:

“A written protocol can describe each action, but it cannot provide the physical and causal context of the full procedure. Early practice on real equipment also competes for furnace time and can consume substrates and process gases. Our goal is to let learners build procedural fluency, recognize safety conditions, and understand how their actions change the process in a repeatable virtual environment.”

## 0:58–1:30 — Orientation and parameters

Visual: Show the procedure panel and parameter setup. Change nanowire radius and target height, then confirm. Hold long enough for the values and simulation-time response to be readable.

Speaker — Barath, appear briefly on camera and then continue as voice-over:

“The experience begins with the learning objective and the growth parameters. The learner sets the desired nanowire radius, height, and catalyst count. The simulation estimates the accelerated training time, validates the selection, and then unlocks the physical procedure. This connects the target material outcome to the decisions that follow.”

## 1:30–2:28 — Prepare the furnace

Visual: Capture these actions as separate clean shots: power on; pick up and snap the substrate to the holder; connect the feed rod; slide the substrate into the quartz tube; close the lid. Show the spatial arrow and confirmation feedback at least once.

Speaker — Barath, voice-over while Yilin performs the interactions:

“Next, the learner prepares the furnace using direct hand interaction. They power the unit, place the substrate on the feed mechanism, connect the rod, align the assembly, and feed it into the quartz tube. Placement guides and constrained motion make the intended manipulation clear without removing the need to perform it. The procedure advances from the actual object state—not from a generic Next button. The furnace must then be closed before heating controls become available.”

## 2:28–3:22 — Gas, temperature, and safety gates

Visual: Rotate the gas valve until the readout reaches the threshold. Set all three temperature zones. Attempt one safe “fault” demonstration if it reads clearly: open the lid or reduce gas flow and show that heating cannot proceed or pauses. Then restore the condition and continue.

Speaker — Barath, voice-over while Yilin operates the valve and controls:

“The process phase makes the safety logic visible. The learner opens the gas flow to the required threshold and sets all three furnace zones. Heating is gated by power, lid position, gas flow, and valid temperatures. If a required condition becomes unsafe, the relevant control is disabled and an active heat cycle pauses. This turns interlocks into something the learner can see, test, and remember.”

## 3:22–4:04 — Heat, soak, and nanowire growth

Visual: Show the three live temperature readouts climbing, the ceramic material beginning to glow, and the growth visualization progressing from catalysts to full nanowires. Use a clean close-up of the progress indicator.

Speaker — Barath, voice-over:

“When all prerequisites are satisfied, the three zones ramp toward their setpoints and the furnace materials provide visible thermal feedback. At target temperature, the nanowire growth model begins automatically while the soak continues. The learner can watch the requested geometry emerge at an accelerated training timescale, linking furnace preparation to the microscopic outcome that would otherwise be difficult to observe.”

## 4:04–4:40 — Cooldown, withdrawal, and reset

Visual: Press heating off; show temperature falling; open the lid only after the safe threshold; reconnect the rod; withdraw the sample; switch off and show the station reset.

Speaker — Barath, voice-over while Yilin completes the procedure:

“The learning cycle does not stop when growth finishes. The learner turns heating off, waits for a safe withdrawal temperature, opens the furnace, reconnects the feed rod, and removes the substrate along the same constrained path. Powering down resets the station for another run, making deliberate repetition part of the design.”

## 4:40–5:24 — State-adaptive guidance and hologram

Visual: Show the current-step panel, spatial arrow, highlighted target, and at least two different hologram/compositor focus states. If the external receiver is not stable, show only the validated four-view compositor output and label it “procedure-synchronized hologram output.”

Speaker — Yilin, on camera for the first sentence, then voice-over over her headset view:

“As I move through the procedure, the guidance follows my current task. The instruction panel tells me what to do, the spatial arrow identifies the relevant object or control, and audio and visual feedback confirm when I complete it. If I attempt a step before the required conditions are safe, the procedure does not advance. The same state also controls the synchronized four-view compositor, so an instructor or teammate can follow the part of the process I am working on.”

## 5:24–6:02 — Implementation and innovation

Visual: Use a clean four-part overlay while corresponding footage continues: “Hand tracking → procedure state → physical simulation → multimodal feedback.” Finish on the hologram output or a wide lab shot.

Speaker — Barath, voice-over with a brief on-camera lead-in:

“Behind Yilin’s experience is a configurable procedure manager that acts as the source of truth. LabSpace VR is built in Unity 6 for Meta Quest using Meta XR, XR Hands, and the XR Interaction Toolkit. The prototype combines direct manipulation, safety gates, deterministic gas and thermal behavior, accelerated growth visualization, and a WebRTC-ready hologram compositor. The innovation is the integration of embodied interaction, process causality, and state-adaptive guidance into one complete laboratory learning cycle.”

## 6:02–6:32 — Educational value and close

Visual: End with three on-screen proof points: “13 guided steps,” “3 coordinated heating zones,” and “4 synchronized hologram views.” Add the repository link and the team/affiliation.

Speaker — Professor, on camera. End on a wide shot with all three team members if possible:

“The result is a functional rehearsal environment designed for safe, repeatable, and scalable laboratory preparation. Learners can practice without physical consumables, receive text, spatial, visual, and audio cues, and adjust the simulation speed. Our next validation step will measure sequence accuracy, safety errors, completion, and retention before and after VR rehearsal. LabSpace VR helps learners practice the whole process—so their first real run is not their first meaningful attempt.”

On-screen closing text:

LabSpace VR Team — Northeastern University  
Professor [full name] • Barath Balamurugan • Yilin [last name]  
github.com/SINRG-Lab/lab-space-vr

## Recording guardrails

- Call the current guidance “rules-based, state-adaptive guidance.” Do not call it AI unless an actual AI feature is implemented and visible before submission.
- Show the physical hologram receiver only if it connects reliably in one take. Otherwise show the validated four-view compositor and describe it precisely.
- Do not use the older prototype clips as final hero footage; capture the current polished OTF scene end to end.
- Keep the headset view steady, with 2–4 seconds of clean footage before and after every interaction.
- Show at least one failed or blocked safety condition; judges need to see that the gates change behavior.
- Add burned-in captions and verify every UI label is readable on a laptop screen.
- Record each speaker on the same microphone, in the same room position, or match loudness and noise reduction during editing.
- Capture a five-second closing shot with all three people; it gives the entry a clear team identity without spending presentation time on introductions.

## Sources

- IEEE competition requirements and judging criteria: https://metaversereality.ieee.org/competition/
- Official rules: https://metaversereality.ieee.org/wp-content/uploads/2026/04/IEEE-FD-and-YP-2026-Metaverse-Grand-Challenge-for-Simulation-Based-Learning-Final-R2.pdf
- Project implementation: repository README, `Assets/Scripts/FurnaceProcedureManager.cs`, and `Packages/manifest.json`
