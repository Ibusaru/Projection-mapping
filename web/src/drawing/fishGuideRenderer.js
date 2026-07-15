import { createFishSilhouettePath } from "../config/fishSilhouette";
import {
  fishGuideCanvasSize,
  getFishEyePath,
  getFishScaleCoveragePath,
  getFishScaleEntries,
} from "./fishPatternPaths";

function isScaleInsideCoverage(context, coveragePath, outlinePoints) {
  return outlinePoints.every(({ x, y }) => context.isPointInPath(coveragePath, x, y));
}

export function renderFishGuideLayer(patternId, { lineWidth, strokeStyle }) {
  const canvas = document.createElement("canvas");
  canvas.width = fishGuideCanvasSize.width;
  canvas.height = fishGuideCanvasSize.height;
  const context = canvas.getContext("2d");

  context.save();
  context.clip(createFishSilhouettePath());
  context.lineCap = "round";
  context.lineJoin = "round";
  context.lineWidth = lineWidth;
  context.strokeStyle = strokeStyle;

  if (patternId === "scales") {
    const scaleCoveragePath = getFishScaleCoveragePath();

    getFishScaleEntries()
      .filter(({ outlinePoints }) => isScaleInsideCoverage(context, scaleCoveragePath, outlinePoints))
      .forEach(({ path: scalePath }) => {
        context.globalCompositeOperation = "destination-out";
        context.fill(scalePath);
        context.globalCompositeOperation = "source-over";
        context.stroke(scalePath);
      });

    context.globalCompositeOperation = "source-over";
    context.stroke(getFishEyePath());
  }

  context.restore();
  return canvas;
}
