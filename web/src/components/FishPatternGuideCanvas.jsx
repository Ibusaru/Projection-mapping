import React, { useEffect, useRef } from "react";
import { createFishSilhouettePath, fishCanvasSize } from "../config/fishSilhouette";
import { renderFishGuideLayer } from "../drawing/fishGuideRenderer";

const GUIDE_STROKE = "rgba(255, 255, 255, 0.62)";
const GUIDE_WIDTH = 3;
const PREVIEW_SCALE = 0.25;
const PREVIEW_SILHOUETTE_FILL = "rgba(9, 31, 42, 0.76)";

function drawGuide(context, patternId) {
  const guideLayer = renderFishGuideLayer(patternId, {
    lineWidth: GUIDE_WIDTH,
    strokeStyle: GUIDE_STROKE,
  });
  context.drawImage(guideLayer, 0, 0);
}

function renderGuideCanvas(canvas, patternId, includeSilhouette, scale) {
  const context = canvas.getContext("2d");
  context.setTransform(1, 0, 0, 1, 0, 0);
  context.clearRect(0, 0, canvas.width, canvas.height);
  context.scale(scale, scale);

  if (includeSilhouette) {
    context.fillStyle = PREVIEW_SILHOUETTE_FILL;
    context.fill(createFishSilhouettePath());
  }

  drawGuide(context, patternId);
}

export function FishPatternGuideCanvas({ patternId }) {
  const canvasRef = useRef(null);

  useEffect(() => {
    renderGuideCanvas(canvasRef.current, patternId, false, 1);
  }, [patternId]);

  return (
    <canvas
      aria-hidden="true"
      className="fish-canvas-layer pattern-guide-canvas"
      height={fishCanvasSize.height}
      ref={canvasRef}
      width={fishCanvasSize.width}
    />
  );
}

export function FishPatternThumbnail({ patternId }) {
  const canvasRef = useRef(null);

  useEffect(() => {
    renderGuideCanvas(canvasRef.current, patternId, true, PREVIEW_SCALE);
  }, [patternId]);

  return (
    <canvas
      aria-hidden="true"
      className="pattern-thumbnail-canvas"
      height={fishCanvasSize.height * PREVIEW_SCALE}
      ref={canvasRef}
      width={fishCanvasSize.width * PREVIEW_SCALE}
    />
  );
}
