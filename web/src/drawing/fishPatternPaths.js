import { fishCanvasSize } from "../config/fishSilhouette.js";

const SCALE_STEP_X = 78;
const SCALE_STEP_Y = 78;
const SCALE_ROW_OFFSET = SCALE_STEP_X / 2;
const SCALE_FIELD_BOUNDS = {
  left: 190,
  right: 760,
  top: 135,
  bottom: 390,
};
const EYE = { x: 180, y: 215, radius: 28 };

let cachedEyePath = null;
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
  path.moveTo(274, 151);
  path.bezierCurveTo(350, 111, 535, 116, 665, 178);
  path.bezierCurveTo(690, 224, 685, 304, 650, 346);
  path.bezierCurveTo(545, 399, 385, 436, 286, 395);
  path.bezierCurveTo(238, 345, 235, 215, 274, 151);
  path.closePath();

  cachedScaleCoveragePath = path;
  return cachedScaleCoveragePath;
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
