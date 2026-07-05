import fs from "node:fs/promises";
import path from "node:path";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const workspace = "C:\\Users\\scrat\\Documents\\創造工学\\work\\presentations\\ocean-poster\\tmp";
const outputDir = "C:\\Users\\scrat\\Documents\\創造工学\\outputs";
const finalPptx = path.join(outputDir, "ocean-poster-shapes.pptx");
const finalPng = path.join(outputDir, "ocean-poster-shapes.png");
const previewPng = path.join(workspace, "preview", "ocean-poster-shapes-preview.png");
const layoutJson = path.join(workspace, "layout", "ocean-poster-shapes.layout.json");

async function writeBlob(filePath, blob) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

function addShape(slide, geometry, position, options = {}) {
  return slide.shapes.add({
    geometry,
    position,
    fill: options.fill ?? "none",
    line: options.line ?? { style: "solid", fill: "none", width: 0 },
    name: options.name,
    borderRadius: options.borderRadius,
    rotation: options.rotation,
  });
}

function addText(slide, options) {
  const box = addShape(
    slide,
    "textbox",
    { left: options.x, top: options.y, width: options.w, height: options.h },
    {
      name: options.name,
      fill: options.fill ?? "none",
      line: options.line ?? { style: "solid", fill: "none", width: 0 },
      borderRadius: options.borderRadius,
    },
  );
  box.text = options.text;
  box.text.style = {
    fontSize: options.size,
    bold: options.bold ?? false,
    color: options.color ?? "#163947",
    alignment: options.align ?? "left",
  };
  return box;
}

function addLine(slide, name, x1, y1, x2, y2, color = "#315866", width = 1.4) {
  return addShape(
    slide,
    "line",
    { left: x1, top: y1, width: x2 - x1, height: y2 - y1 },
    {
      name,
      fill: "none",
      line: { style: "solid", fill: color, width },
    },
  );
}

function addPath(slide, name, points, color, width = 2, fill = "none") {
  const [first, ...rest] = points;
  return slide.shapes.add({
    geometry: "custom",
    name,
    position: { left: 0, top: 0, width: 1080, height: 1536 },
    fill,
    line: { style: "solid", fill: color, width },
    customPaths: [
      {
        width: 1080,
        height: 1536,
        commands: [
          { moveTo: { x: first[0], y: first[1] } },
          ...rest.map(([x, y]) => ({ lineTo: { x, y } })),
        ],
      },
    ],
  });
}

function addSection(slide, { key, label, body, x, y, w, h }) {
  addShape(slide, "rect", { left: x, top: y, width: w, height: h }, {
    name: `${key}-frame`,
    fill: "#fffdf5/78",
    line: { style: "solid", fill: "#4c7780/70", width: 1.3 },
  });
  addText(slide, {
    name: `${key}-label`,
    text: label,
    x: x + 22,
    y: y + 18,
    w: w - 44,
    h: 36,
    size: 28,
    bold: true,
    color: "#183f4b",
  });
  addLine(slide, `${key}-rule`, x + 22, y + 60, x + w - 22, y + 60, "#83a6ac", 1);
  addText(slide, {
    name: `${key}-body`,
    text: body,
    x: x + 22,
    y: y + 74,
    w: w - 44,
    h: h - 92,
    size: 18,
    color: "#31535d",
  });
}

async function main() {
  await fs.mkdir(outputDir, { recursive: true });
  const presentation = Presentation.create({ slideSize: { width: 1080, height: 1536 } });
  const slide = presentation.slides.add();

  slide.background.fill = "#f8f5ea";

  addShape(slide, "rect", { left: 0, top: 182, width: 1080, height: 1040 }, {
    name: "flat-water-field",
    fill: "linear(0deg, #b9e7ee/78 0%, #e6f7f7/68 72%, #f8f5ea/0 100%)",
    line: { style: "solid", fill: "none", width: 0 },
  });

  addPath(slide, "sea-surface-main", [
    [64, 215], [156, 202], [242, 174], [346, 162], [455, 173],
    [560, 196], [668, 202], [760, 185], [850, 158], [1018, 148],
  ], "#1d8ca0", 2.6);
  addPath(slide, "sea-surface-second", [
    [62, 228], [160, 218], [250, 190], [350, 178], [454, 186],
    [560, 210], [666, 215], [758, 198], [852, 174], [1018, 164],
  ], "#4aaec0/80", 1.6);
  addPath(slide, "sea-surface-light", [
    [70, 246], [178, 238], [286, 216], [392, 210], [490, 220],
    [590, 238], [692, 238], [792, 220], [894, 198], [1012, 196],
  ], "#8bcbd3/70", 1.2);

  for (let i = 0; i < 7; i += 1) {
    const x = 120 + i * 128;
    addLine(slide, `water-pencil-line-${i}`, x, 304 + (i % 2) * 16, x + 86, 284 + (i % 3) * 11, "#9cced3/34", 1);
  }

  addText(slide, {
    name: "poster-title",
    text: "海洋環境の観察テーマ",
    x: 86,
    y: 58,
    w: 908,
    h: 74,
    size: 54,
    bold: true,
    color: "#173c46",
    align: "center",
  });
  addText(slide, {
    name: "poster-subtitle",
    text: "研究タイトル・発表者名をここに入力",
    x: 206,
    y: 132,
    w: 668,
    h: 36,
    size: 23,
    color: "#55727a",
    align: "center",
  });

  addText(slide, {
    name: "sea-label",
    text: "海",
    x: 815,
    y: 315,
    w: 88,
    h: 54,
    size: 40,
    bold: true,
    color: "#163f4b",
    align: "center",
  });
  addText(slide, {
    name: "bubble-label",
    text: "気泡",
    x: 890,
    y: 410,
    w: 116,
    h: 42,
    size: 26,
    bold: true,
    color: "#406b73",
    align: "center",
  });

  const bubbles = [
    [930, 305, 10], [962, 328, 16], [912, 365, 7], [984, 384, 10],
    [938, 430, 6], [972, 470, 8], [920, 500, 5],
  ];
  for (const [x, y, s] of bubbles) {
    addShape(slide, "ellipse", { left: x, top: y, width: s, height: s }, {
      name: `bubble-${x}-${y}`,
      fill: "#f8ffff/50",
      line: { style: "solid", fill: "#6baeba/70", width: 1 },
    });
  }

  addSection(slide, {
    key: "background",
    label: "背景",
    body: "調べる海の現象や課題を短くまとめます。",
    x: 86,
    y: 520,
    w: 280,
    h: 178,
  });
  addSection(slide, {
    key: "purpose",
    label: "目的",
    body: "何を明らかにしたいのかを1文で示します。",
    x: 400,
    y: 520,
    w: 280,
    h: 178,
  });
  addSection(slide, {
    key: "method",
    label: "方法",
    body: "観察場所、記録方法、比較する条件を入れます。",
    x: 714,
    y: 520,
    w: 280,
    h: 178,
  });
  addSection(slide, {
    key: "result",
    label: "結果",
    body: "写真・グラフ・観察記録を置き、重要な発見を大きく見せます。",
    x: 150,
    y: 790,
    w: 366,
    h: 192,
  });
  addSection(slide, {
    key: "discussion",
    label: "考察",
    body: "海面、海中、海底の関係から結果の意味を説明します。",
    x: 564,
    y: 790,
    w: 366,
    h: 192,
  });

  addShape(slide, "rect", { left: 260, top: 1060, width: 560, height: 62 }, {
    name: "center-placeholder",
    fill: "#fffdf5/54",
    line: { style: "dashed", fill: "#5f8c94/70", width: 1.1 },
  });
  addText(slide, {
    name: "center-placeholder-text",
    text: "写真・グラフ・調査地点図をここに追加",
    x: 282,
    y: 1074,
    w: 516,
    h: 32,
    size: 22,
    color: "#4f747b",
    align: "center",
  });

  addPath(slide, "seabed-boundary", [
    [48, 1210], [160, 1198], [280, 1214], [410, 1198], [548, 1184],
    [674, 1192], [798, 1218], [930, 1222], [1034, 1214],
  ], "#6d6257", 1.8);
  addShape(slide, "rect", { left: 0, top: 1220, width: 1080, height: 316 }, {
    name: "sand-bed",
    fill: "#e9dfc9/82",
    line: { style: "solid", fill: "none", width: 0 },
  });
  for (let i = -1; i < 11; i += 1) {
    const x = i * 124;
    addLine(slide, `seabed-hatch-${i}`, x, 1496, x + 320, 1240, "#9b8c78/58", 1.2);
  }
  for (const rock of [
    [56, 1288, 110, 58], [172, 1318, 76, 42], [796, 1294, 112, 60],
    [920, 1346, 70, 36], [402, 1392, 62, 30], [544, 1328, 88, 42],
  ]) {
    addShape(slide, "ellipse", { left: rock[0], top: rock[1], width: rock[2], height: rock[3] }, {
      name: `rock-${rock[0]}`,
      fill: "#a7aaa1/72",
      line: { style: "solid", fill: "#6e756f/80", width: 1 },
    });
  }
  for (const [baseX, baseY, color] of [
    [96, 1322, "#667f76"], [188, 1340, "#a47c7f"], [854, 1328, "#b9868a"], [944, 1310, "#6f8f85"],
  ]) {
    addLine(slide, `coral-main-${baseX}`, baseX, baseY + 82, baseX, baseY, color, 1.5);
    addLine(slide, `coral-left-${baseX}`, baseX, baseY + 40, baseX - 30, baseY + 14, color, 1.4);
    addLine(slide, `coral-right-${baseX}`, baseX, baseY + 52, baseX + 28, baseY + 26, color, 1.4);
    addLine(slide, `coral-top-${baseX}`, baseX, baseY + 18, baseX + 18, baseY - 8, color, 1.2);
  }

  addText(slide, {
    name: "seabed-label",
    text: "海底（サンゴ礁・砂・岩）",
    x: 310,
    y: 1330,
    w: 460,
    h: 54,
    size: 30,
    bold: true,
    color: "#544c42",
    align: "center",
    fill: "#fffdf5/72",
    line: { style: "solid", fill: "#9c8e7b/65", width: 1 },
  });

  const slidePng = await presentation.export({ slide, format: "png", scale: 2 });
  await writeBlob(finalPng, slidePng);
  await writeBlob(previewPng, slidePng);
  const layout = await slide.export({ format: "layout" });
  await fs.mkdir(path.dirname(layoutJson), { recursive: true });
  await fs.writeFile(layoutJson, await layout.text(), "utf8");

  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(finalPptx);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
