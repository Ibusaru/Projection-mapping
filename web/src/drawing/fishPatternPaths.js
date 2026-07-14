import { fishCanvasSize } from "../config/fishSilhouette.js";

const SCALE_STEP_X = 78;
const SCALE_STEP_Y = 78;
const SCALE_ROW_OFFSET = SCALE_STEP_X / 2;
const SCALE_FIELD_BOUNDS = {
  left: 250,
  right: 900,
  top: 70,
  bottom: 460,
};
const EYE = { x: 180, y: 215, radius: 28 };

let cachedEyePath = null;
let cachedDorsalFinGuidePath = null;
let cachedScaleCoveragePath = null;
let cachedScalePaths = null;

function createScalePath(centerX, centerY) {
  const path = new Path2D();

  path.moveTo(centerX - 47, centerY - 42);
  path.bezierCurveTo(
    centerX - 11,
    centerY - 59,
    centerX + 40,
    centerY - 51,
    centerX + 54,
    centerY - 15
  );
  path.bezierCurveTo(
    centerX + 68,
    centerY + 24,
    centerX + 38,
    centerY + 55,
    centerX,
    centerY + 59
  );
  path.bezierCurveTo(
    centerX - 31,
    centerY + 60,
    centerX - 49,
    centerY + 36,
    centerX - 45,
    centerY + 8
  );
  path.bezierCurveTo(
    centerX - 41,
    centerY - 13,
    centerX - 38,
    centerY - 31,
    centerX - 47,
    centerY - 42
  );
  path.closePath();
  return path;
}

export function getFishScalePaths() {
  if (cachedScalePaths) return cachedScalePaths;

  const entries = [];
  let rowIndex = 0;

  for (
    let centerY = SCALE_FIELD_BOUNDS.top;
    centerY <= SCALE_FIELD_BOUNDS.bottom;
    centerY += SCALE_STEP_Y
  ) {
    const rowOffset = rowIndex % 2 === 0 ? 0 : SCALE_ROW_OFFSET;

    for (
      let centerX = SCALE_FIELD_BOUNDS.left + rowOffset;
      centerX <= SCALE_FIELD_BOUNDS.right;
      centerX += SCALE_STEP_X
    ) {
      entries.push({ centerX, centerY, path: createScalePath(centerX, centerY) });
    }

    rowIndex += 1;
  }

  entries.sort((left, right) => right.centerX - left.centerX || right.centerY - left.centerY);
  cachedScalePaths = entries.map((entry) => entry.path);
  return cachedScalePaths;
}

export function getFishScaleCoveragePath() {
  if (cachedScaleCoveragePath) return cachedScaleCoveragePath;

  const path = new Path2D();
  path.moveTo(299, 110);
  path.lineTo(530, 168);
  path.bezierCurveTo(625, 141, 735, 145, 798, 157);
  path.bezierCurveTo(838, 211, 840, 309, 795, 366);
  path.bezierCurveTo(655, 414, 470, 455, 334, 414);
  path.bezierCurveTo(310, 351, 300, 185, 299, 110);
  path.closePath();

  cachedScaleCoveragePath = path;
  return cachedScaleCoveragePath;
}

export function getFishDorsalFinGuidePath() {
  if (cachedDorsalFinGuidePath) return cachedDorsalFinGuidePath;

  const path = new Path2D();

  // A straight base and three broad rays make four large, easy-to-fill sections.
  path.moveTo(299, 110);
  path.lineTo(530, 168);

  path.moveTo(352, 66);
  path.lineTo(370, 128);

  path.moveTo(405, 45);
  path.lineTo(425, 142);

  path.moveTo(459, 72);
  path.lineTo(480, 155);

  cachedDorsalFinGuidePath = path;
  return cachedDorsalFinGuidePath;
}

export function getFishEyePath() {
  if (cachedEyePath) return cachedEyePath;

  const path = new Path2D();
  path.moveTo(EYE.x + EYE.radius, EYE.y);
  path.ellipse(EYE.x, EYE.y, EYE.radius, EYE.radius, 0, 0, Math.PI * 2);
  path.closePath();
  cachedEyePath = path;
  return cachedEyePath;
}

export const fishGuideCanvasSize = fishCanvasSize;
