import fs from "node:fs/promises";
import path from "node:path";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const W = 1586.67;
const H = 2244;
const root = process.cwd();
const out = process.argv[2] || path.resolve(root, "../../../../outputs/ocean-ao.pptx");

const assets = {
  seaFloor: "source/template-inspect/assets/ppt/media/image20.png",
  pinkFish: "source/template-inspect/assets/ppt/media/image17.png",
  greenFish: "source/template-inspect/assets/ppt/media/image19.png",
  qr: "source/template-inspect/assets/ppt/media/image15.png",
  visitor: "source/template-inspect/assets/ppt/media/image16.png",
  react: "source/template-inspect/assets/ppt/media/image5.png",
  supabase: "source/template-inspect/assets/ppt/media/image8.png",
  unity: "source/template-inspect/assets/ppt/media/image9.png",
  blender: "source/template-inspect/assets/ppt/media/image10.png",
  obs: "source/template-inspect/assets/ppt/media/image11.png",
  resolume: "source/template-inspect/assets/ppt/media/image12.jpeg",
  projector: "source/template-inspect/assets/ppt/media/image13.png",
  surface: "reference/template-inspect/assets/ppt/media/image1.jpg",
  rays: "reference/template-inspect/assets/ppt/media/image12.png",
  diver: "reference/template-inspect/assets/ppt/media/image11.png",
};

const color = {
  navy: "#003B5C",
  deep: "#001B40",
  teal: "#0E9BB5",
  aqua: "#CFF8FF",
  paper: "#F1FCFF",
  sand: "#F7E8C5",
  coral: "#FF7A7A",
  ink: "#043047",
  white: "#FFFFFF",
};

async function bytes(rel) {
  return fs.readFile(path.join(root, rel));
}

function typeFor(rel) {
  const ext = path.extname(rel).toLowerCase();
  if (ext === ".jpg" || ext === ".jpeg") return "image/jpeg";
  return "image/png";
}

async function image(slide, rel, frame, options = {}) {
  return slide.images.add({
    blob: await bytes(rel),
    contentType: typeFor(rel),
    alt: options.alt || path.basename(rel),
    fit: options.fit || "contain",
    position: frame,
    ...(options.geometry ? { geometry: options.geometry } : {}),
    ...(options.borderRadius !== undefined ? { borderRadius: options.borderRadius } : {}),
    ...(options.crop ? { crop: options.crop } : {}),
  });
}

function shape(slide, frame, fill, line = "transparent", geometry = "rect") {
  return slide.shapes.add({
    geometry,
    position: frame,
    fill,
    line: { style: "solid", fill: line, width: line === "transparent" ? 0 : 2 },
  });
}

function text(slide, value, frame, style = {}) {
  const box = shape(slide, frame, style.fill || "transparent", style.line || "transparent");
  box.text = value;
  box.text.style = {
    fontSize: style.size || 32,
    bold: Boolean(style.bold),
    color: style.color || color.ink,
    typeface: style.face || "Yu Gothic",
    alignment: style.align || "left",
  };
  box.text.verticalAlignment = style.valign || "top";
  box.text.insets = style.insets || { left: 0, right: 0, top: 0, bottom: 0 };
  return box;
}

function panel(slide, frame, options = {}) {
  const p = shape(
    slide,
    frame,
    options.fill || "#FFFFFF/88",
    options.line || "#7DDCE8/80",
    "roundRect",
  );
  p.borderRadius = options.radius ?? 8;
  return p;
}

function pill(slide, label, x, y, w, fill = color.deep) {
  const p = shape(slide, { left: x, top: y, width: w, height: 48 }, fill, "transparent", "roundRect");
  p.borderRadius = 8;
  text(slide, label, { left: x + 18, top: y + 7, width: w - 36, height: 36 }, {
    size: 23,
    bold: true,
    color: color.white,
    align: "center",
  });
}

async function addOceanBase(slide, depth = "mid") {
  const gradient =
    depth === "surface"
      ? "linear(180deg, #BDF3FF 0%, #4FC8DA 40%, #08799B 100%)"
      : depth === "deep"
        ? "linear(180deg, #0B7A98 0%, #01496B 38%, #001B40 100%)"
        : "linear(180deg, #7DDFEB 0%, #16A8C1 48%, #035381 100%)";
  slide.background.fill = gradient;
  await image(slide, assets.rays, { left: 0, top: 0, width: W, height: 940 }, { fit: "cover", alt: "water light rays" });
  await image(slide, assets.seaFloor, { left: 0, top: H - 720, width: W, height: 720 }, { fit: "cover", alt: "illustrated sea floor" });
  shape(slide, { left: 0, top: 0, width: W, height: H }, depth === "deep" ? "#001B40/28" : "#FFFFFF/0");
}

function sectionLabel(slide, label, x, y) {
  text(slide, label, { left: x, top: y, width: 380, height: 44 }, {
    size: 26,
    bold: true,
    color: color.teal,
  });
  shape(slide, { left: x, top: y + 48, width: 120, height: 5 }, color.coral);
}

function bulletList(slide, items, frame, size = 30) {
  text(slide, items.map((item) => `・${item}`).join("\n"), frame, {
    size,
    color: color.ink,
    fill: "transparent",
  });
}

async function slideOverview(presentation) {
  const slide = presentation.slides.add();
  await addOceanBase(slide, "surface");
  await image(slide, assets.surface, { left: 0, top: 0, width: W, height: 520 }, {
    fit: "cover",
    alt: "ocean surface with reef",
  });
  shape(slide, { left: 0, top: 0, width: W, height: 620 }, "linear(180deg, #FFFFFF/10 0%, #00A6C8/48 78%, #007EA0/0 100%)");

  text(slide, "デジタル水族館", { left: 96, top: 134, width: 980, height: 112 }, {
    size: 86,
    bold: true,
    color: color.white,
  });
  text(slide, "来場者がつくった魚が、海中空間に泳ぎ出す参加型プロジェクション", { left: 102, top: 264, width: 1140, height: 72 }, {
    size: 34,
    color: color.white,
  });
  pill(slide, "C1", 1260, 146, 130, color.coral);
  text(slide, "俵伊吹 / 長谷川煌輔 / 中嶋華鈴 / 三木快和", { left: 103, top: 350, width: 1000, height: 52 }, {
    size: 28,
    color: color.white,
  });

  panel(slide, { left: 86, top: 610, width: 1414, height: 390 });
  sectionLabel(slide, "目的", 126, 654);
  text(slide, "オープンキャンパスで来場した中学生に、舞鶴高専の魅力や、ものづくり・プログラミングの楽しさを体験してもらう。入学後の学生生活や将来のイメージまで持ってもらえる展示を目指した。", {
    left: 126,
    top: 728,
    width: 1320,
    height: 180,
  }, { size: 36, color: color.ink });

  panel(slide, { left: 86, top: 1060, width: 690, height: 540 });
  sectionLabel(slide, "プロジェクトの核", 126, 1106);
  bulletList(slide, [
    "海中をテーマにした来場者参加型のプロジェクションマッピング",
    "Webアプリで魚を選び、色や柄を自由にカスタマイズ",
    "制作した作品を来場者自身に体験してもらう",
  ], { left: 126, top: 1190, width: 600, height: 250 }, 31);
  await image(slide, assets.pinkFish, { left: 452, top: 1380, width: 250, height: 150 }, { alt: "custom fish" });
  await image(slide, assets.greenFish, { left: 180, top: 1380, width: 260, height: 150 }, { alt: "custom fish" });

  panel(slide, { left: 810, top: 1060, width: 690, height: 540 });
  sectionLabel(slide, "開発環境", 850, 1106);
  const logos = [
    [assets.react, "React", 862, 1200],
    [assets.supabase, "Supabase", 1078, 1200],
    [assets.unity, "Unity", 1294, 1200],
    [assets.blender, "Blender", 862, 1416],
    [assets.obs, "OBS Studio", 1078, 1416],
    [assets.resolume, "Resolume", 1294, 1416],
  ];
  for (const [asset, label, x, y] of logos) {
    shape(slide, { left: x, top: y, width: 154, height: 154 }, color.white, "#B8E8F0", "roundRect").borderRadius = 8;
    await image(slide, asset, { left: x + 17, top: y + 17, width: 120, height: 120 }, { alt: label });
  }

  panel(slide, { left: 86, top: 1660, width: 1414, height: 360 }, { fill: "#E9FBFF/92" });
  sectionLabel(slide, "システム構成", 126, 1706);
  const steps = [
    ["Webサイト\nReact", assets.react],
    ["データ保存\nSupabase", assets.supabase],
    ["海中表示\nUnity / Blender", assets.unity],
    ["映像調整\nOBS / Resolume", assets.projector],
    ["教室へ投影\nプロジェクター", assets.projector],
  ];
  let x = 126;
  for (let i = 0; i < steps.length; i++) {
    const [label, asset] = steps[i];
    shape(slide, { left: x, top: 1808, width: 210, height: 138 }, "#FFFFFF/92", "#6BD7E3", "roundRect").borderRadius = 8;
    await image(slide, asset, { left: x + 18, top: 1824, width: 60, height: 60 }, { alt: label });
    text(slide, label, { left: x + 86, top: 1820, width: 112, height: 90 }, { size: 22, bold: true, color: color.ink });
    if (i < steps.length - 1) {
      text(slide, "→", { left: x + 226, top: 1838, width: 54, height: 60 }, { size: 44, bold: true, color: color.teal, align: "center" });
    }
    x += 268;
  }
}

async function slideExperience(presentation) {
  const slide = presentation.slides.add();
  await addOceanBase(slide, "mid");
  text(slide, "体験の流れ", { left: 92, top: 92, width: 980, height: 78 }, {
    size: 66,
    bold: true,
    color: color.white,
  });
  text(slide, "QRから参加し、自分の魚をつくって、投影された水族館へ放流する。", { left: 96, top: 184, width: 1130, height: 58 }, {
    size: 32,
    color: color.white,
  });

  panel(slide, { left: 82, top: 318, width: 1422, height: 650 }, { fill: "#FFFFFF/88" });
  const flow = [
    ["01", "QRコードを\n読み取る", assets.qr],
    ["02", "Webサイトに\nアクセス", assets.visitor],
    ["03", "ニックネームを\n入力", assets.visitor],
    ["04", "魚を選び\nカスタマイズ", assets.pinkFish],
    ["05", "放流", assets.greenFish],
    ["06", "投影空間に\n反映", assets.projector],
  ];
  const gap = 32;
  const cardW = (1422 - 80 - gap * 2) / 3;
  for (let i = 0; i < flow.length; i++) {
    const col = i % 3;
    const row = Math.floor(i / 3);
    const left = 122 + col * (cardW + gap);
    const top = 372 + row * 270;
    shape(slide, { left, top, width: cardW, height: 220 }, i % 2 ? "#E6FAFF/92" : "#FFFFFF/94", "#7DDCE8", "roundRect").borderRadius = 8;
    text(slide, flow[i][0], { left: left + 24, top: top + 22, width: 80, height: 46 }, { size: 30, bold: true, color: color.coral });
    await image(slide, flow[i][2], { left: left + 28, top: top + 78, width: 106, height: 106 }, { alt: flow[i][1] });
    text(slide, flow[i][1], { left: left + 160, top: top + 62, width: cardW - 190, height: 118 }, { size: 32, bold: true, color: color.ink });
  }

  panel(slide, { left: 82, top: 1038, width: 660, height: 480 }, { fill: "#F5FDFF/90" });
  sectionLabel(slide, "制作物", 122, 1084);
  bulletList(slide, [
    "Blenderで作った魚・海底オブジェクト",
    "背景と投影用の映像演出",
    "Webで魚を編集する参加画面",
  ], { left: 122, top: 1166, width: 560, height: 160 }, 30);
  await image(slide, assets.pinkFish, { left: 182, top: 1348, width: 220, height: 130 }, { alt: "pink fish" });
  await image(slide, assets.greenFish, { left: 420, top: 1340, width: 230, height: 140 }, { alt: "green fish" });

  panel(slide, { left: 782, top: 1038, width: 722, height: 480 }, { fill: "#F5FDFF/90" });
  sectionLabel(slide, "今後の展望", 822, 1084);
  bulletList(slide, [
    "魚の種類を増やし、より多彩な水族館を表現する",
    "背景演出を追加し、臨場感のある空間にする",
    "餌やりなどの機能で、体験型コンテンツに発展させる",
    "同時参加しやすいシステムへ改良する",
  ], { left: 822, top: 1166, width: 620, height: 260 }, 29);

  panel(slide, { left: 82, top: 1588, width: 1422, height: 340 }, { fill: "#EAFBFF/94" });
  sectionLabel(slide, "役割分担", 122, 1632);
  const roles = [
    "俵：プログラム、Webアプリ作成",
    "中嶋：海底オブジェクト、映像投影",
    "長谷川：装飾オブジェクト",
    "三木：魚オブジェクト",
  ];
  for (let i = 0; i < roles.length; i++) {
    const left = 122 + (i % 2) * 670;
    const top = 1718 + Math.floor(i / 2) * 90;
    text(slide, roles[i], { left, top, width: 610, height: 48 }, { size: 30, bold: true, color: color.ink });
  }
}

async function slideClose(presentation) {
  const slide = presentation.slides.add();
  await addOceanBase(slide, "deep");
  await image(slide, assets.diver, { left: 1020, top: 120, width: 390, height: 590 }, {
    fit: "cover",
    geometry: "roundRect",
    borderRadius: 8,
    alt: "diver underwater",
  });
  text(slide, "海の中に、\n自分の魚が泳ぎ出す", { left: 96, top: 150, width: 880, height: 240 }, {
    size: 76,
    bold: true,
    color: color.white,
  });
  text(slide, "デジタル水族館は、ものづくりとプログラミングを「見て終わり」ではなく、自分の手で変化させる体験として届ける展示。", {
    left: 102,
    top: 438,
    width: 880,
    height: 150,
  }, { size: 34, color: "#DFFBFF" });

  panel(slide, { left: 110, top: 810, width: 1366, height: 560 }, { fill: "#FFFFFF/88", line: "#7DDCE8/80" });
  text(slide, "伝えたいこと", { left: 166, top: 872, width: 520, height: 58 }, {
    size: 42,
    bold: true,
    color: color.teal,
  });
  bulletList(slide, [
    "高専で学ぶ技術は、身近な体験を自分たちでつくる力になる",
    "Web・データベース・3D・投影をつなげることで、教室が水族館になる",
    "来場者の操作がすぐ映像に反映されることで、参加する楽しさが生まれる",
  ], { left: 166, top: 962, width: 1240, height: 240 }, 34);

  shape(slide, { left: 160, top: 1250, width: 1190, height: 2 }, "#28C4D8/80");
  const stack = ["Webアプリ", "データ保存", "3D表示", "映像投影", "参加体験"];
  for (let i = 0; i < stack.length; i++) {
    const left = 180 + i * 235;
    shape(slide, { left, top: 1280, width: 188, height: 52 }, i === 4 ? color.coral : color.teal, "transparent", "roundRect").borderRadius = 8;
    text(slide, stack[i], { left: left + 8, top: 1290, width: 172, height: 32 }, { size: 23, bold: true, color: color.white, align: "center" });
  }

  text(slide, "ご覧いただきありがとうございました", { left: 116, top: 1530, width: 1050, height: 76 }, {
    size: 50,
    bold: true,
    color: color.white,
  });
  text(slide, "C1 デジタル水族館", { left: 120, top: 1616, width: 600, height: 46 }, {
    size: 30,
    color: "#DFFBFF",
  });
  await image(slide, assets.pinkFish, { left: 988, top: 1530, width: 250, height: 150 }, { alt: "pink fish" });
  await image(slide, assets.greenFish, { left: 1196, top: 1600, width: 260, height: 160 }, { alt: "green fish" });
}

async function exportPreviews(presentation) {
  const previewDir = path.join(root, "qa", "final-preview");
  await fs.mkdir(previewDir, { recursive: true });
  let index = 1;
  for (const slide of presentation.slides.items) {
    const png = await presentation.export({ slide, format: "png", scale: 1 });
    await fs.writeFile(path.join(previewDir, `slide-${String(index).padStart(2, "0")}.png`), Buffer.from(await png.arrayBuffer()));
    const layout = await slide.export({ format: "layout" });
    await fs.writeFile(path.join(previewDir, `slide-${String(index).padStart(2, "0")}.layout.json`), await layout.text());
    index += 1;
  }
  const montage = await presentation.export({ format: "webp", montage: true, scale: 1 });
  await fs.writeFile(path.join(previewDir, "montage.webp"), Buffer.from(await montage.arrayBuffer()));
}

async function main() {
  await fs.mkdir(path.dirname(out), { recursive: true });
  const presentation = Presentation.create({ slideSize: { width: W, height: H } });
  await slideOverview(presentation);
  await slideExperience(presentation);
  await slideClose(presentation);
  await exportPreviews(presentation);
  const pptx = await PresentationFile.exportPptx(presentation);
  await pptx.save(out);
  console.log(out);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
