import { createFishSilhouettePath } from "../config/fishSilhouette";
import {
  fishGuideCanvasSize,
  getFishDorsalFinGuidePath,
  getFishEyePath,
  getFishScaleCoveragePath,
  getFishScalePaths,
} from "./fishPatternPaths";

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
    context.save();
    context.clip(getFishScaleCoveragePath());

    getFishScalePaths().forEach((scalePath) => {
      context.globalCompositeOperation = "destination-out";
      context.fill(scalePath);
      context.globalCompositeOperation = "source-over";
      context.stroke(scalePath);
    });

    context.restore();
    context.globalCompositeOperation = "source-over";
    context.stroke(getFishDorsalFinGuidePath());
    context.stroke(getFishEyePath());
  }

  context.restore();
  return canvas;
}
