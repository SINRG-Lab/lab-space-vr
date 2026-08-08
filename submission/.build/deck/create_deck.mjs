import fs from "node:fs/promises";
import path from "node:path";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const ROOT = "/Users/barath/BarathMac/XERT/lab-space-vr";
const BUILD = path.join(ROOT, "submission/.build/deck");
const OUT = path.join(ROOT, "submission/LabSpace_VR_IEEE_Metaverse_2026.pptx");

const C = {
  canvas: "#FFFFFF",
  ink: "#050505",
  muted: "#5D6670",
  panel: "#EDEDED",
  panel2: "#F6F7F8",
  rule: "#B8BCC4",
  cyan: "#6DCBF4",
  cyanStrong: "#1BA7D1",
  dark: "#101417",
  white: "#FFFFFF",
};

const FONT = "Helvetica Neue";
const W = 1280;
const H = 720;
const M = 54;

async function bytes(file) {
  return new Uint8Array(await fs.readFile(file));
}

function addBox(slide, name, left, top, width, height, fill = "none", lineFill = "none", lineWidth = 0, radius = 0) {
  return slide.shapes.add({
    geometry: radius ? "roundRect" : "rect",
    name,
    position: { left, top, width, height },
    fill,
    line: { style: "solid", fill: lineFill, width: lineWidth },
    ...(radius ? { borderRadius: radius } : {}),
  });
}

function addText(slide, name, text, left, top, width, height, opts = {}) {
  const shape = slide.shapes.add({
    geometry: "textbox",
    name,
    position: { left, top, width, height },
    fill: opts.fill ?? "none",
    line: { style: "solid", fill: opts.lineFill ?? "none", width: opts.lineWidth ?? 0 },
    ...(opts.radius ? { borderRadius: opts.radius } : {}),
  });
  shape.text = text;
  shape.text.style = {
    fontSize: opts.fontSize ?? 24,
    typeface: FONT,
    color: opts.color ?? C.ink,
    bold: opts.bold ?? false,
    alignment: opts.align ?? "left",
    verticalAlignment: opts.valign ?? "top",
    autoFit: opts.autoFit ?? "shrinkText",
    wrap: "square",
    lineSpacing: opts.lineSpacing ?? 1.02,
    insets: opts.insets ?? { top: 0, right: 0, bottom: 0, left: 0 },
  };
  return shape;
}

async function addImage(slide, name, file, alt, left, top, width, height, fit = "cover", crop = undefined, radius = 0) {
  return slide.images.add({
    blob: await bytes(file),
    contentType: file.toLowerCase().endsWith(".png") ? "image/png" : "image/jpeg",
    alt,
    fit,
    position: { left, top, width, height },
    ...(crop ? { crop } : {}),
    ...(radius ? { geometry: "roundRect", borderRadius: radius } : {}),
  });
}

function addTitle(slide, text, number) {
  addText(slide, `slide-${number}-title`, text, M, 36, W - M * 2, 76, {
    fontSize: 48,
    bold: true,
    lineSpacing: 0.94,
  });
  addBox(slide, `slide-${number}-accent`, M, 122, 96, 5, C.cyanStrong);
}

function addFooter(slide, number) {
  addText(slide, `slide-${number}-footer-left`, "LABSPACE VR  •  ADVANCED LEARNING", M, 681, 430, 18, {
    fontSize: 12,
    bold: true,
    color: C.muted,
    valign: "middle",
  });
  addText(slide, `slide-${number}-footer-number`, String(number).padStart(2, "0"), 1172, 676, 54, 22, {
    fontSize: 14,
    bold: true,
    color: C.muted,
    align: "right",
    valign: "middle",
  });
}

function notes(slide, talkTrack, sources) {
  slide.speakerNotes.textFrame.setText(`${talkTrack}\n\n[Sources]\n${sources.map((s) => `- ${s}`).join("\n")}`);
  slide.speakerNotes.setVisible(true);
}

async function makeDeck() {
  await fs.mkdir(BUILD, { recursive: true });
  await fs.mkdir(path.dirname(OUT), { recursive: true });
  const deck = Presentation.create({ slideSize: { width: W, height: H } });

  // Slide 1 — Codex Grid layout 08: half text, half image.
  {
    const slide = deck.slides.add();
    slide.background.fill = C.canvas;
    addText(slide, "s1-eyebrow", "2026 IEEE METAVERSE GRAND CHALLENGE", M, 42, 560, 26, {
      fontSize: 18,
      bold: true,
      color: C.cyanStrong,
    });
    addText(slide, "s1-title", "Practice nanowire growth before touching the furnace", M, 110, 560, 238, {
      fontSize: 70,
      bold: true,
      lineSpacing: 0.88,
      autoFit: "shrinkText",
    });
    addText(slide, "s1-subtitle", "LabSpace VR transforms a complex split-tube furnace protocol into guided, hand-tracked practice.", M, 386, 530, 116, {
      fontSize: 28,
      color: C.muted,
      lineSpacing: 1.05,
    });
    addBox(slide, "s1-theme-rule", M, 540, 84, 5, C.cyanStrong);
    addText(slide, "s1-theme", "THEME 3\nAdvanced Learning in Educational or Classroom Environments", M, 560, 530, 72, {
      fontSize: 20,
      bold: true,
      lineSpacing: 1.0,
    });
    addText(slide, "s1-author", "LabSpace VR Team  •  Northeastern University", M, 654, 530, 24, {
      fontSize: 16,
      color: C.muted,
    });
    addBox(slide, "s1-image-backing", 642, 38, 584, 604, C.dark, C.rule, 1, 14);
    await addImage(slide, "s1-hero", path.join(ROOT, "Images/IsoView.jpeg"), "Isometric render of the split-tube furnace", 642, 38, 584, 604, "cover", { left: 0.02, top: 0, right: 0.03, bottom: 0 }, 14);
    notes(
      slide,
      "Open with the learning promise: this is not a passive equipment tour. It is hands-on rehearsal of the decisions and safety sequence behind nanowire growth. The professor establishes the educational need, Barath Balamurugan presents the implementation as main developer, and Yilin demonstrates the learner experience as a project contributor.",
      [
        "IEEE challenge theme and submission requirements: https://metaversereality.ieee.org/competition/",
        "Project description: README.md in the Lab Space VR repository",
        "Hero render: Images/IsoView.jpeg (project-owned asset)",
      ],
    );
  }

  // Slide 2 — Codex Grid layout 10: evidence plus interpretation.
  {
    const slide = deck.slides.add();
    slide.background.fill = C.canvas;
    addTitle(slide, "A protocol alone cannot teach coordinated lab judgment", 2);
    addText(slide, "s2-lead", "Learners must manage sequence, safety conditions, and process response at the same time.", M, 166, 520, 108, {
      fontSize: 31,
      bold: true,
      lineSpacing: 1.0,
    });
    addText(slide, "s2-b1-num", "01", M, 314, 56, 42, { fontSize: 25, bold: true, color: C.cyanStrong });
    addText(slide, "s2-b1", "Load and position a substrate through a constrained feed mechanism.", 118, 307, 450, 64, { fontSize: 22 });
    addText(slide, "s2-b2-num", "02", M, 410, 56, 42, { fontSize: 25, bold: true, color: C.cyanStrong });
    addText(slide, "s2-b2", "Coordinate gas flow, three temperature zones, ramp, soak, and growth.", 118, 403, 450, 66, { fontSize: 22 });
    addText(slide, "s2-b3-num", "03", M, 508, 56, 42, { fontSize: 25, bold: true, color: C.cyanStrong });
    addText(slide, "s2-b3", "Cool to a safe state, withdraw the sample, and reset correctly.", 118, 501, 450, 66, { fontSize: 22 });
    addBox(slide, "s2-callout-bg", M, 596, 520, 58, C.panel2);
    addText(slide, "s2-callout", "VR makes the whole cycle repeatable before scarce equipment and consumables are involved.", 72, 610, 484, 34, { fontSize: 18, bold: true, color: C.dark });
    addBox(slide, "s2-image-bg", 618, 166, 608, 488, C.dark, C.rule, 1, 12);
    await addImage(slide, "s2-furnace", path.join(ROOT, "Images/FrontView.jpeg"), "Front render of the open split-tube furnace", 618, 166, 608, 488, "cover", { left: 0.02, top: 0.05, right: 0.02, bottom: 0.08 }, 12);
    addFooter(slide, 2);
    notes(
      slide,
      "Frame the educational gap. The difficult part is not recognizing the equipment; it is coordinating interdependent actions and understanding why the sequence matters. Virtual rehearsal avoids using furnace time, process gases, and substrates during early practice.",
      [
        "Procedure and implementation summary: README.md",
        "Procedure state logic: Assets/Scripts/FurnaceProcedureManager.cs",
        "Furnace render: Images/FrontView.jpeg (project-owned asset)",
      ],
    );
  }

  // Slide 3 — Codex Grid layout 17: three-stage timeline.
  {
    const slide = deck.slides.add();
    slide.background.fill = C.canvas;
    addTitle(slide, "Thirteen guided steps turn the protocol into embodied practice", 3);
    const xs = [54, 456, 858];
    const frameFiles = [
      path.join(ROOT, "submission/.build/assets/parameters-start.jpg"),
      path.join(ROOT, "submission/.build/assets/furnace-controls.jpg"),
      path.join(ROOT, "submission/.build/assets/growth-end.jpg"),
    ];
    const alts = ["Nanowire parameter setup in VR", "Hand interacting with furnace controls", "Nanowire growth visualization"];
    for (let i = 0; i < 3; i++) {
      addBox(slide, `s3-image-bg-${i}`, xs[i], 168, 348, 158, C.dark, C.rule, 1, 8);
      await addImage(slide, `s3-image-${i}`, frameFiles[i], alts[i], xs[i], 168, 348, 158, "cover", undefined, 8);
    }
    addBox(slide, "s3-timeline", 54, 356, 1150, 2, C.rule);
    const phases = [
      { x: xs[0], n: "1–6", title: "Prepare", body: "Confirm growth parameters\nPower on • load • connect\nFeed substrate • close lid" },
      { x: xs[1], n: "7–10", title: "Run the process", body: "Set gas flow and 3 zones\nRamp and soak\nObserve nanowire growth" },
      { x: xs[2], n: "11–13", title: "Finish safely", body: "Cool below threshold\nOpen furnace • withdraw\nPower off and reset" },
    ];
    phases.forEach((p, i) => {
      addBox(slide, `s3-node-${i}`, p.x, 348, 18, 18, C.cyanStrong);
      addText(slide, `s3-range-${i}`, p.n, p.x, 386, 100, 28, { fontSize: 18, bold: true, color: C.cyanStrong });
      addText(slide, `s3-phase-${i}`, p.title, p.x, 420, 340, 42, { fontSize: 31, bold: true });
      addText(slide, `s3-body-${i}`, p.body, p.x, 476, 340, 116, { fontSize: 21, color: C.muted, lineSpacing: 1.12 });
    });
    addText(slide, "s3-bottom", "Each step advances only when its required interaction and safety gates are satisfied.", 54, 622, 1150, 36, { fontSize: 21, bold: true, align: "center" });
    addFooter(slide, 3);
    notes(
      slide,
      "Walk through the learning cycle in three phases. Emphasize that completion is driven by actual state: a placed substrate, a closed lid, sufficient gas flow, valid setpoints, safe cooldown, and successful withdrawal—not by pressing a Next button.",
      [
        "Target procedure and safety gates: README.md",
        "Configurable step list: Assets/Scripts/FurnaceProcedureManager.cs",
        "Prototype frames extracted from Videos/SimDemov1.mp4, Videos/Demo_v1.mp4, and Videos/SImDemov3.mp4",
      ],
    );
  }

  // Slide 4 — state-aware architecture with connectors created first.
  {
    const slide = deck.slides.add();
    slide.background.fill = C.canvas;
    addTitle(slide, "Guidance adapts to the learner’s current state", 4);

    const positions = [
      { left: 54, top: 240, width: 232, height: 156 },
      { left: 338, top: 240, width: 232, height: 156 },
      { left: 622, top: 240, width: 232, height: 156 },
      { left: 906, top: 240, width: 320, height: 156 },
    ];
    for (let i = 0; i < positions.length - 1; i++) {
      addBox(slide, `s4-connector-${i}`, positions[i].left + positions[i].width, 316, positions[i + 1].left - (positions[i].left + positions[i].width), 3, C.cyanStrong);
    }
    const nodeCopy = [
      ["HAND-TRACKED ACTION", "Grab, rotate, press, snap, feed"],
      ["PROCEDURE STATE", "Validate gates and unlock only what is relevant"],
      ["REAL-TIME RESPONSE", "Gas, heat, materials, growth, cooldown"],
      ["MULTIMODAL GUIDANCE", "Instruction panel, spatial arrow, audio feedback, synchronized hologram view"],
    ];
    positions.forEach((p, i) => {
      addBox(slide, `s4-node-${i}`, p.left, p.top, p.width, p.height, i === 3 ? C.dark : C.panel2, i === 3 ? C.dark : C.rule, 1, 8);
      addText(slide, `s4-node-title-${i}`, nodeCopy[i][0], p.left + 18, p.top + 20, p.width - 36, 42, {
        fontSize: 18,
        bold: true,
        color: i === 3 ? C.cyan : C.cyanStrong,
      });
      addText(slide, `s4-node-body-${i}`, nodeCopy[i][1], p.left + 18, p.top + 70, p.width - 36, 68, {
        fontSize: i === 3 ? 19 : 20,
        bold: i === 3,
        color: i === 3 ? C.white : C.ink,
        lineSpacing: 1.02,
      });
    });
    addText(slide, "s4-rule-based", "Rules-based adaptation—not an AI claim", 54, 444, 470, 28, { fontSize: 18, bold: true, color: C.muted });
    addText(slide, "s4-why", "Unsafe or out-of-sequence actions cannot advance the task. The same configurable procedure state also changes control availability, progress, simulation feedback, and camera focus.", 54, 480, 790, 108, { fontSize: 25, bold: true, lineSpacing: 1.02 });
    addBox(slide, "s4-holo-bg", 906, 448, 320, 168, C.dark, C.rule, 1, 8);
    await addImage(slide, "s4-holo", path.join(ROOT, "Artifacts/Blender/four_projector_trapezoid_prism_preview.png"), "Concept visualization of the synchronized four-view hologram display", 906, 448, 320, 168, "cover", { left: 0.05, top: 0.11, right: 0.03, bottom: 0.09 }, 8);
    addText(slide, "s4-stack", "Unity 6 • Meta XR • XR Hands/XRI • URP • WebRTC compositor", 54, 630, 790, 30, { fontSize: 18, bold: true, color: C.muted });
    addFooter(slide, 4);
    notes(
      slide,
      "Explain the implementation from input to feedback. The procedure manager is the source of truth. This is currently a rules-based adaptive system: guidance changes with the learner's state, while invalid conditions pause or block the process. The four-view compositor mirrors the active step; describe physical receiver streaming only if it is stable in the final recording.",
      [
        "Project technology stack: Packages/manifest.json and ProjectSettings/ProjectVersion.txt",
        "State-aware procedure implementation: Assets/Scripts/FurnaceProcedureManager.cs",
        "Hologram behavior: README.md and Assets/Scripts/Hologram/",
        "Hologram concept image: Artifacts/Blender/four_projector_trapezoid_prism_preview.png (project-owned asset)",
      ],
    );
  }

  // Slide 5 — Codex Grid layout 19: three metric-led proof points.
  {
    const slide = deck.slides.add();
    slide.background.fill = C.canvas;
    addTitle(slide, "The prototype makes laboratory preparation safer, repeatable, and scalable", 5);
    addText(slide, "s5-lead", "Built for Meta Quest as a functional rehearsal environment—not a passive walkthrough.", M, 156, 920, 48, { fontSize: 27, bold: true });
    const stats = [
      { x: 54, stat: "13", label: "guided steps", body: "A complete prepare → process → finish learning cycle" },
      { x: 456, stat: "3", label: "temperature zones", body: "Coordinated setpoints, ramp, soak, cooldown, and safety checks" },
      { x: 858, stat: "4", label: "hologram views", body: "A synchronized output that refocuses with the active procedure step" },
    ];
    stats.forEach((s, i) => {
      addBox(slide, `s5-panel-${i}`, s.x, 244, 348, 272, C.panel2);
      addText(slide, `s5-stat-${i}`, s.stat, s.x + 24, 270, 120, 84, { fontSize: 72, bold: true, color: C.cyanStrong, valign: "middle" });
      addText(slide, `s5-label-${i}`, s.label, s.x + 24, 360, 300, 40, { fontSize: 27, bold: true });
      addText(slide, `s5-body-${i}`, s.body, s.x + 24, 416, 300, 74, { fontSize: 20, color: C.muted, lineSpacing: 1.06 });
    });
    addText(slide, "s5-impact", "Multimodal cues  •  adjustable simulation speed  •  no physical consumables during practice", 54, 550, 1172, 38, { fontSize: 22, bold: true, align: "center" });
    const repo = addText(slide, "s5-repo", "Source & project updates: github.com/SINRG-Lab/lab-space-vr", 54, 608, 1172, 32, { fontSize: 19, bold: true, color: C.cyanStrong, align: "center" });
    repo.text.get("github.com/SINRG-Lab/lab-space-vr").link = { uri: "https://github.com/SINRG-Lab/lab-space-vr", isExternal: true };
    addText(slide, "s5-author", "LabSpace VR Team  •  Northeastern University", 54, 649, 1172, 24, { fontSize: 16, color: C.muted, align: "center" });
    addFooter(slide, 5);
    notes(
      slide,
      "The professor closes on the educational value. The prototype supports repeatable practice with multiple cue types and adjustable simulation speed. Be explicit that learning-effectiveness validation is the next research step: compare sequence accuracy, safety errors, task completion, and retention before and after VR rehearsal.",
      [
        "Prototype counts and accessibility features: README.md and Assets/Scripts/FurnaceProcedureManager.cs",
        "Project source link: https://github.com/SINRG-Lab/lab-space-vr",
        "IEEE judging priorities: https://metaversereality.ieee.org/competition/",
      ],
    );
  }

  for (const [i, slide] of deck.slides.items.entries()) {
    const stem = `slide-${String(i + 1).padStart(2, "0")}`;
    const png = await deck.export({ slide, format: "png", scale: 1 });
    await fs.writeFile(path.join(BUILD, `${stem}.png`), new Uint8Array(await png.arrayBuffer()));
    const layout = await slide.export({ format: "layout" });
    await fs.writeFile(path.join(BUILD, `${stem}.layout.json`), await layout.text());
  }
  const montage = await deck.export({ format: "webp", montage: true, scale: 1 });
  await fs.writeFile(path.join(BUILD, "deck-montage.webp"), new Uint8Array(await montage.arrayBuffer()));
  const snapshot = await deck.inspect({ kind: "slide,textbox,shape,image,notes", maxChars: 30000 });
  await fs.writeFile(path.join(BUILD, "deck-inspect.ndjson"), snapshot.ndjson);
  const pptx = await PresentationFile.exportPptx(deck);
  await pptx.save(OUT);
  console.log(OUT);
}

makeDeck().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
