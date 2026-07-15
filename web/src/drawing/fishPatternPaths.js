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
const SCALE_START = { x: -47, y: -42 };
const SCALE_CURVES = [
  [{ x: -11, y: -59 }, { x: 40, y: -51 }, { x: 54, y: -15 }],
  [{ x: 68, y: 24 }, { x: 38, y: 55 }, { x: 0, y: 59 }],
  [{ x: -31, y: 60 }, { x: -49, y: 36 }, { x: -45, y: 8 }],
  [{ x: -41, y: -13 }, { x: -38, y: -31 }, SCALE_START],
];
const SCALE_CURVE_SAMPLE_STEPS = 12;
const EYE = { x: 180, y: 215, radius: 18 };

let cachedEyePath = null;
let cachedScaleCoveragePath = null;
let cachedScaleEntries = null;

function getCubicBezierPoint(start, controlOne, controlTwo, end, progress) {
  const inverse = 1 - progress;
  const inverseSquared = inverse * inverse;
  const progressSquared = progress * progress;

  return {
    x: inverseSquared * inverse * start.x
      + 3 * inverseSquared * progress * controlOne.x
      + 3 * inverse * progressSquared * controlTwo.x
      + progressSquared * progress * end.x,
    y: inverseSquared * inverse * start.y
      + 3 * inverseSquared * progress * controlOne.y
      + 3 * inverse * progressSquared * controlTwo.y
      + progressSquared * progress * end.y,
  };
}

function createScaleEntry(centerX, centerY) {
  const path = new Path2D();
  const outlinePoints = [];
  let start = SCALE_START;

  path.moveTo(centerX + start.x, centerY + start.y);

  SCALE_CURVES.forEach(([controlOne, controlTwo, end]) => {
    path.bezierCurveTo(
      centerX + controlOne.x,
      centerY + controlOne.y,
      centerX + controlTwo.x,
      centerY + controlTwo.y,
      centerX + end.x,
      centerY + end.y
    );

    for (let step = 1; step <= SCALE_CURVE_SAMPLE_STEPS; step += 1) {
      const point = getCubicBezierPoint(
        start,
        controlOne,
        controlTwo,
        end,
        step / SCALE_CURVE_SAMPLE_STEPS
      );
      outlinePoints.push({ x: centerX + point.x, y: centerY + point.y });
    }

    start = end;
  });

  path.closePath();
  return { centerX, centerY, outlinePoints, path };
}

export function getFishScaleEntries() {
  if (cachedScaleEntries) return cachedScaleEntries;

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
      entries.push(createScaleEntry(centerX, centerY));
    }

    rowIndex += 1;
  }

  entries.sort((left, right) => right.centerX - left.centerX || right.centerY - left.centerY);
  cachedScaleEntries = entries;
  return cachedScaleEntries;
}

export function getFishScaleCoveragePath() {
  if (cachedScaleCoveragePath) return cachedScaleCoveragePath;

  const path = new Path2D();
  path.moveTo(330, 78);
  path.bezierCurveTo(470, 43, 665, 108, 798, 157);
  path.bezierCurveTo(838, 211, 840, 309, 795, 366);
  path.bezierCurveTo(655, 414, 470, 455, 334, 414);
  path.bezierCurveTo(288, 351, 286, 160, 330, 78);
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
