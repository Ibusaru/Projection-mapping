import fs from "node:fs/promises";
import path from "node:path";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const workspace = "C:\\Users\\scrat\\Documents\\創造工学\\work\\presentations\\ocean-poster\\tmp";
const outputDir = "C:\\Users\\scrat\\Documents\\創造工学\\outputs";
const backgroundPath = path.join(workspace, "assets", "ocean-poster-background.png");
const finalPptx = path.join(outputDir, "ocean-poster-template.pptx");
const finalPng = path.join(outputDir, "ocean-poster-template.png");
const previewPng = path.join(workspace, "preview", "ocean-poster-template-preview.png");
const layoutJson = path.join(workspace, "layout", "ocean-poster-template.layout.json");

async function writeBlob(filePath, blob) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, new Uint8Array(await blob.arrayBuffer()));
}

async function readImageBlob(filePath) {
  const bytes = await fs.readFile(filePath);
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
}

function addText(slide, { name, text, x, y, w, h, size, color = "#07354c", bold = false, align = "left", fill = "none", line = "none" }) {
  const shape = slide.shapes.add({
    geometry: "textbox",
    name,
    position: { left: x, top: y, width: w, height: h },
    fill,
    line: { style: "solid", fill: line, width: line === "none" ? 0 : 1 },
  });
  shape.text = text;
  shape.text.style = { fontSize: size, bold, color, alignment: align };
  return shape;
}

function addBox(slide, { name, x, y, w, h, fill = "white/62", line = "#c7e4ea/70" }) {
  return slide.shapes.add({
    geometry: "roundRect",
    name,
    position: { left: x, top: y, width: w, height: h },
    fill,
    line: { style: "solid", fill: line, width: 1 },
    borderRadius: 8,
  });
}

async function main() {
  await fs.mkdir(outputDir, { recursive: true });

  const presentation = Presentation.create({
    slideSize: { width: 1080, height: 1536 },
  });

  const slide = presentation.slides.add();
  slide.background.fill = "#eaf8fb";

  const background = await readImageBlob(backgroundPath);
  slide.images.add({
    blob: background,
    contentType: "image/png",
    alt: "海面、海中、海底を縦構図で描いたポスター背景",
    fit: "cover",
    position: { left: 0, top: 0, width: 1080, height: 1536 },
  });

  addText(slide, {
    name: "poster-title",
    text: "海洋環境の観察テーマ",
    x: 84,
    y: 58,
    w: 912,
    h: 82,
    size: 58,
    color: "#063348",
    bold: true,
    align: "center",
  });

  addText(slide, {
    name: "poster-subtitle",
    text: "研究タイトル・発表者名をここに入力",
    x: 186,
    y: 138,
    w: 708,
    h: 38,
    size: 24,
    color: "#2b6475",
    align: "center",
  });

  addText(slide, {
    name: "sea-label",
    text: "海",
    x: 820,
    y: 340,
    w: 110,
    h: 54,
    size: 42,
    color: "#064861",
    bold: true,
    align: "center",
  });

  addText(slide, {
    name: "bubble-label",
    text: "気泡",
    x: 905,
    y: 442,
    w: 116,
    h: 42,
    size: 28,
    color: "#2a6a7a",
    bold: true,
    align: "center",
  });

  const cards = [
    { name: "background", title: "背景", body: "この研究で注目する海の現象や課題を短く整理します。", x: 92, y: 560 },
    { name: "purpose", title: "目的", body: "観察・比較・評価したい問いを1文で示します。", x: 394, y: 560 },
    { name: "method", title: "方法", body: "調査場所、測定方法、記録する項目を入れます。", x: 696, y: 560 },
    { name: "result", title: "結果", body: "図表や写真を中央に置き、重要な発見を大きく示します。", x: 170, y: 830 },
    { name: "discussion", title: "考察", body: "海面・海中・海底の関係から、結果の意味を説明します。", x: 550, y: 830 },
  ];

  for (const card of cards) {
    addBox(slide, {
      name: `${card.name}-box`,
      x: card.x,
      y: card.y,
      w: card.name === "result" || card.name === "discussion" ? 360 : 252,
      h: card.name === "result" || card.name === "discussion" ? 176 : 186,
    });
    addText(slide, {
      name: `${card.name}-heading`,
      text: card.title,
      x: card.x + 24,
      y: card.y + 22,
      w: card.name === "result" || card.name === "discussion" ? 312 : 204,
      h: 36,
      size: 29,
      color: "#063348",
      bold: true,
    });
    addText(slide, {
      name: `${card.name}-body`,
      text: card.body,
      x: card.x + 24,
      y: card.y + 70,
      w: card.name === "result" || card.name === "discussion" ? 312 : 204,
      h: card.name === "result" || card.name === "discussion" ? 86 : 104,
      size: 19,
      color: "#285666",
    });
  }

  addText(slide, {
    name: "seabed-label",
    text: "海底（サンゴ礁・砂・岩）",
    x: 320,
    y: 1328,
    w: 440,
    h: 52,
    size: 32,
    color: "#53443a",
    bold: true,
    align: "center",
    fill: "white/58",
    line: "#d3bfae/70",
  });

  addText(slide, {
    name: "small-note",
    text: "写真・グラフ・調査地点図を中央の余白に追加できます",
    x: 235,
    y: 1060,
    w: 610,
    h: 44,
    size: 22,
    color: "#1f6878",
    align: "center",
    fill: "white/34",
    line: "#caeef2/60",
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
