export const DEFAULT_FISH_PATTERN_ID = "scales";

export const fishPatternCategories = [
  { id: "fish", label: "魚らしい模様" },
  { id: "ocean", label: "海の模様" },
  { id: "cute", label: "かわいい模様" },
];

export const fishPatternOptions = [
  {
    id: "none",
    label: "なし",
    category: "free",
    description: "下絵を使わず自由に描く",
  },
  {
    id: "scales",
    label: "うろこ",
    category: "fish",
    description: "大きなうろこを一枚ずつ塗る",
  },
  {
    id: "stripes",
    label: "ゆるいしま",
    category: "fish",
    description: "曲がった太いしまを塗り分ける",
  },
  {
    id: "patchwork",
    label: "パッチワーク",
    category: "fish",
    description: "大きな八つの区画を塗り分ける",
  },
  {
    id: "waves",
    label: "なみ",
    category: "ocean",
    description: "三本の波を好きな色で重ねる",
  },
  {
    id: "bubbles",
    label: "あわ",
    category: "ocean",
    description: "大きさの違う泡を塗る",
  },
  {
    id: "coral",
    label: "サンゴ",
    category: "ocean",
    description: "三本のサンゴを塗り分ける",
  },
  {
    id: "hearts",
    label: "ハート",
    category: "cute",
    description: "大きなハートを五つ並べる",
  },
  {
    id: "stars",
    label: "ほし",
    category: "cute",
    description: "大きな星を五つ並べる",
  },
  {
    id: "dots",
    label: "水玉",
    category: "cute",
    description: "八つの水玉を塗り分ける",
  },
];

const optionsById = new Map(fishPatternOptions.map((option) => [option.id, option]));

function addScale(path, centerX, centerY, radiusX, radiusY) {
  path.moveTo(centerX - radiusX, centerY - radiusY * 0.28);
  path.bezierCurveTo(
    centerX - radiusX * 0.5,
    centerY - radiusY,
    centerX + radiusX * 0.5,
    centerY - radiusY,
    centerX + radiusX,
    centerY - radiusY * 0.28
  );
  path.bezierCurveTo(
    centerX + radiusX * 0.82,
    centerY + radiusY * 0.52,
    centerX + radiusX * 0.35,
    centerY + radiusY,
    centerX,
    centerY + radiusY
  );
  path.bezierCurveTo(
    centerX - radiusX * 0.35,
    centerY + radiusY,
    centerX - radiusX * 0.82,
    centerY + radiusY * 0.52,
    centerX - radiusX,
    centerY - radiusY * 0.28
  );
  path.closePath();
}

function addCurvedBand(path, centerX, width, topY, bottomY, bend) {
  const left = centerX - width * 0.5;
  const right = centerX + width * 0.5;

  path.moveTo(left, topY);
  path.bezierCurveTo(left + bend, topY + 70, left - bend, bottomY - 70, left, bottomY);
  path.lineTo(right, bottomY);
  path.bezierCurveTo(right - bend, bottomY - 70, right + bend, topY + 70, right, topY);
  path.closePath();
}

function addWaveBand(path, centerY, amplitude, thickness, phase = 0) {
  const startX = 250;
  const endX = 830;
  const segments = 30;

  for (let step = 0; step <= segments; step += 1) {
    const progress = step / segments;
    const x = startX + (endX - startX) * progress;
    const y = centerY + Math.sin(progress * Math.PI * 3 + phase) * amplitude - thickness * 0.5;
    if (step === 0) path.moveTo(x, y);
    else path.lineTo(x, y);
  }

  for (let step = segments; step >= 0; step -= 1) {
    const progress = step / segments;
    const x = startX + (endX - startX) * progress;
    const y = centerY + Math.sin(progress * Math.PI * 3 + phase) * amplitude + thickness * 0.5;
    path.lineTo(x, y);
  }

  path.closePath();
}

function addHeart(path, centerX, centerY, size) {
  const top = centerY - size * 0.3;

  path.moveTo(centerX, centerY + size * 0.72);
  path.bezierCurveTo(
    centerX - size * 0.18,
    centerY + size * 0.48,
    centerX - size,
    centerY,
    centerX - size * 0.62,
    top
  );
  path.bezierCurveTo(
    centerX - size * 0.35,
    centerY - size * 0.8,
    centerX - size * 0.04,
    centerY - size * 0.55,
    centerX,
    centerY - size * 0.2
  );
  path.bezierCurveTo(
    centerX + size * 0.04,
    centerY - size * 0.55,
    centerX + size * 0.35,
    centerY - size * 0.8,
    centerX + size * 0.62,
    top
  );
  path.bezierCurveTo(
    centerX + size,
    centerY,
    centerX + size * 0.18,
    centerY + size * 0.48,
    centerX,
    centerY + size * 0.72
  );
  path.closePath();
}

function addStar(path, centerX, centerY, outerRadius, innerRadius) {
  for (let pointIndex = 0; pointIndex < 10; pointIndex += 1) {
    const radius = pointIndex % 2 === 0 ? outerRadius : innerRadius;
    const angle = -Math.PI / 2 + (pointIndex * Math.PI) / 5;
    const x = centerX + Math.cos(angle) * radius;
    const y = centerY + Math.sin(angle) * radius;
    if (pointIndex === 0) path.moveTo(x, y);
    else path.lineTo(x, y);
  }
  path.closePath();
}

function addCircle(path, centerX, centerY, radius) {
  path.moveTo(centerX + radius, centerY);
  path.ellipse(centerX, centerY, radius, radius, 0, 0, Math.PI * 2);
  path.closePath();
}

function addCoral(path, centerX, baseY, scale) {
  const points = [
    [-18, 70],
    [-22, 22],
    [-50, -4],
    [-50, -25],
    [-36, -31],
    [-20, -12],
    [-16, -50],
    [-7, -67],
    [3, -60],
    [2, -26],
    [25, -51],
    [40, -47],
    [43, -33],
    [23, -19],
    [48, -7],
    [47, 9],
    [29, 13],
    [15, 1],
    [19, 70],
  ];

  points.forEach(([localX, localY], index) => {
    const x = centerX + localX * scale;
    const y = baseY + (localY - 70) * scale;
    if (index === 0) path.moveTo(x, y);
    else path.lineTo(x, y);
  });
  path.closePath();
}

function createScalesPath() {
  const path = new Path2D();
  const rows = [
    { y: 175, xs: [330, 470, 610, 750] },
    { y: 270, xs: [300, 440, 580, 720] },
    { y: 365, xs: [350, 490, 630, 770] },
  ];

  rows.forEach(({ y, xs }) => xs.forEach((x) => addScale(path, x, y, 50, 48)));
  return path;
}

function createStripesPath() {
  const path = new Path2D();
  [
    [310, 46, 155, 365, 16],
    [420, 62, 140, 375, -18],
    [535, 50, 135, 375, 20],
    [650, 66, 145, 370, -17],
    [770, 46, 160, 360, 14],
  ].forEach((args) => addCurvedBand(path, ...args));
  return path;
}

function createPatchworkPath() {
  const path = new Path2D();

  path.moveTo(255, 175);
  path.bezierCurveTo(360, 135, 690, 135, 820, 180);
  path.bezierCurveTo(845, 235, 840, 320, 800, 360);
  path.bezierCurveTo(650, 385, 380, 385, 265, 350);
  path.bezierCurveTo(235, 300, 232, 225, 255, 175);
  path.closePath();

  path.moveTo(390, 151);
  path.bezierCurveTo(372, 220, 414, 300, 390, 373);
  path.moveTo(535, 145);
  path.bezierCurveTo(560, 215, 510, 305, 535, 380);
  path.moveTo(680, 150);
  path.bezierCurveTo(655, 220, 705, 300, 680, 378);
  path.moveTo(242, 262);
  path.bezierCurveTo(380, 235, 670, 285, 835, 258);

  return path;
}

function createWavesPath() {
  const path = new Path2D();
  addWaveBand(path, 185, 18, 38, 0);
  addWaveBand(path, 270, 20, 42, 1.2);
  addWaveBand(path, 350, 16, 36, 2.2);
  return path;
}

function createBubblesPath() {
  const path = new Path2D();
  [
    [320, 205, 54],
    [455, 325, 62],
    [575, 190, 46],
    [700, 300, 58],
    [800, 205, 34],
    [335, 340, 27],
  ].forEach(([x, y, radius]) => addCircle(path, x, y, radius));
  return path;
}

function createCoralPath() {
  const path = new Path2D();
  addCoral(path, 350, 365, 1.05);
  addCoral(path, 550, 365, 0.9);
  addCoral(path, 745, 365, 1.08);
  return path;
}

function createHeartsPath() {
  const path = new Path2D();
  [
    [330, 205, 44],
    [500, 200, 48],
    [675, 205, 44],
    [415, 325, 46],
    [605, 325, 46],
  ].forEach((args) => addHeart(path, ...args));
  return path;
}

function createStarsPath() {
  const path = new Path2D();
  [
    [330, 200, 48, 22],
    [500, 195, 52, 24],
    [675, 205, 47, 21],
    [415, 325, 48, 22],
    [610, 325, 50, 23],
  ].forEach((args) => addStar(path, ...args));
  return path;
}

function createDotsPath() {
  const path = new Path2D();
  [
    [300, 195, 34],
    [440, 190, 31],
    [580, 198, 35],
    [720, 192, 30],
    [360, 325, 31],
    [500, 320, 35],
    [640, 330, 31],
    [780, 315, 34],
  ].forEach(([x, y, radius]) => addCircle(path, x, y, radius));
  return path;
}

const pathFactories = {
  bubbles: createBubblesPath,
  coral: createCoralPath,
  dots: createDotsPath,
  hearts: createHeartsPath,
  patchwork: createPatchworkPath,
  scales: createScalesPath,
  stars: createStarsPath,
  stripes: createStripesPath,
  waves: createWavesPath,
};

export function normalizeFishPatternId(patternId) {
  return optionsById.has(patternId) ? patternId : DEFAULT_FISH_PATTERN_ID;
}

export function getFishPatternOption(patternId) {
  return optionsById.get(normalizeFishPatternId(patternId));
}

export function createFishPatternPath(patternId) {
  const normalizedId = normalizeFishPatternId(patternId);
  return pathFactories[normalizedId]?.() ?? new Path2D();
}
