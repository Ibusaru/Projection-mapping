import React, { forwardRef, useEffect, useImperativeHandle, useRef, useState } from "react";
import { Eraser, PaintBucket, Paintbrush, Pipette, Redo2, Undo2 } from "lucide-react";

const CANVAS_WIDTH = 1024;
const CANVAS_HEIGHT = 512;
const MAX_HISTORY_STEPS = 30;
const EMPTY_ALPHA_THRESHOLD = 24;
const SEAM_ALPHA_THRESHOLD = 180;
const SEAM_GROW_STEPS = 2;
const FISH_SILHOUETTE_FILL = "rgba(9, 31, 42, 0.86)";
const FISH_EXPORT_BASE_FILL = "rgba(255, 255, 255, 1)";
let paintMaskPixels = null;

function addBodyPath(path) {
  path.moveTo(92, 252);
  path.bezierCurveTo(104, 190, 178, 145, 275, 134);
  path.bezierCurveTo(304, 102, 348, 82, 406, 84);
  path.bezierCurveTo(466, 86, 512, 119, 520, 152);
  path.bezierCurveTo(546, 134, 586, 128, 620, 146);
  path.bezierCurveTo(648, 160, 674, 187, 704, 202);
  path.bezierCurveTo(730, 216, 758, 216, 786, 222);
  path.bezierCurveTo(800, 238, 807, 256, 804, 275);
  path.bezierCurveTo(770, 284, 736, 286, 710, 304);
  path.bezierCurveTo(660, 342, 584, 368, 470, 379);
  path.bezierCurveTo(340, 393, 200, 372, 130, 316);
  path.bezierCurveTo(104, 294, 91, 273, 92, 252);
  path.closePath();
}

function addTailPath(path) {
  path.moveTo(780, 220);
  path.bezierCurveTo(820, 198, 870, 178, 935, 181);
  path.bezierCurveTo(972, 184, 991, 205, 988, 244);
  path.bezierCurveTo(985, 270, 985, 294, 990, 326);
  path.bezierCurveTo(952, 341, 900, 335, 850, 313);
  path.bezierCurveTo(820, 300, 796, 288, 780, 286);
  path.closePath();
}

function addFinPaths(path) {
  path.moveTo(228, 360);
  path.bezierCurveTo(260, 424, 342, 416, 374, 348);
  path.lineTo(314, 360);
  path.bezierCurveTo(286, 366, 254, 366, 228, 360);
  path.closePath();

  path.moveTo(502, 354);
  path.bezierCurveTo(540, 430, 626, 438, 668, 360);
  path.lineTo(604, 370);
  path.bezierCurveTo(568, 370, 532, 364, 502, 354);
  path.closePath();

  path.moveTo(476, 346);
  path.bezierCurveTo(440, 392, 432, 460, 464, 500);
  path.bezierCurveTo(496, 505, 519, 486, 518, 448);
  path.bezierCurveTo(516, 404, 504, 370, 476, 346);
  path.closePath();
}

function createPaintableFishPath(context) {
  const path = new Path2D();

  addBodyPath(path);
  addTailPath(path);
  addFinPaths(path);

  if (context) {
    context.fill(path);
  }

  return path;
}

function createFishPath(context) {
  const path = createPaintableFishPath();

  if (context) {
    context.fill(path);
  }

  return path;
}

function drawSilhouette(context, fillStyle = FISH_SILHOUETTE_FILL) {
  context.save();
  context.fillStyle = fillStyle;
  createFishPath(context);
  context.restore();
}

function getCanvasPoint(canvas, event) {
  const rect = canvas.getBoundingClientRect();
  return {
    x: ((event.clientX - rect.left) / rect.width) * CANVAS_WIDTH,
    y: ((event.clientY - rect.top) / rect.height) * CANVAS_HEIGHT,
  };
}

function toHexColor(red, green, blue) {
  return `#${[red, green, blue]
    .map((value) => value.toString(16).padStart(2, "0"))
    .join("")}`;
}

function hexToRgba(hex) {
  const clean = hex.replace("#", "");
  const value = Number.parseInt(clean.length === 3
    ? clean.split("").map((char) => char + char).join("")
    : clean, 16);

  return {
    r: (value >> 16) & 255,
    g: (value >> 8) & 255,
    b: value & 255,
    a: 255,
  };
}

function getColorDistance(data, offset, target) {
  const redDistance = data[offset] - target.r;
  const greenDistance = data[offset + 1] - target.g;
  const blueDistance = data[offset + 2] - target.b;

  return redDistance * redDistance + greenDistance * greenDistance + blueDistance * blueDistance;
}

function colorMatches(data, offset, target, tolerance) {
  const colorDistance = getColorDistance(data, offset, target);
  const colorLimit = Math.max(1, tolerance) * Math.max(1, tolerance) * 3;

  return colorDistance <= colorLimit;
}

function isTransparentFillTarget(target) {
  return target.a <= EMPTY_ALPHA_THRESHOLD;
}

function isFillCandidate(data, offset, target, tolerance, transparentTarget) {
  if (transparentTarget) {
    const alphaLimit = Math.min(96, EMPTY_ALPHA_THRESHOLD + tolerance);
    return data[offset + 3] <= alphaLimit;
  }

  return data[offset + 3] > 0 && colorMatches(data, offset, target, tolerance);
}

function paintPixel(data, offset, maskAlpha, replacement, transparentTarget) {
  const alpha = transparentTarget
    ? Math.round(replacement.a * (maskAlpha / 255))
    : Math.min(replacement.a, maskAlpha);
  const changed = data[offset] !== replacement.r
    || data[offset + 1] !== replacement.g
    || data[offset + 2] !== replacement.b
    || data[offset + 3] !== alpha;

  data[offset] = replacement.r;
  data[offset + 1] = replacement.g;
  data[offset + 2] = replacement.b;
  data[offset + 3] = alpha;

  return changed;
}

function addSoftSeamPixel(index, data, mask, filled, filledPixels, nextFrontier) {
  if (filledPixels[index]) {
    return;
  }

  const offset = index * 4;
  if (mask[offset + 3] === 0 || data[offset + 3] > SEAM_ALPHA_THRESHOLD) {
    return;
  }

  filledPixels[index] = 1;
  filled.push(index);
  nextFrontier.push(index);
}

function closeSoftSeams(data, mask, filled, filledPixels) {
  let frontier = filled.slice();

  for (let step = 0; step < SEAM_GROW_STEPS && frontier.length > 0; step += 1) {
    const nextFrontier = [];

    for (let i = 0; i < frontier.length; i += 1) {
      const index = frontier[i];
      const x = index % CANVAS_WIDTH;
      const y = Math.floor(index / CANVAS_WIDTH);

      if (x > 0) addSoftSeamPixel(index - 1, data, mask, filled, filledPixels, nextFrontier);
      if (x < CANVAS_WIDTH - 1) addSoftSeamPixel(index + 1, data, mask, filled, filledPixels, nextFrontier);
      if (y > 0) addSoftSeamPixel(index - CANVAS_WIDTH, data, mask, filled, filledPixels, nextFrontier);
      if (y < CANVAS_HEIGHT - 1) addSoftSeamPixel(index + CANVAS_WIDTH, data, mask, filled, filledPixels, nextFrontier);
    }

    frontier = nextFrontier;
  }
}

function getPaintMaskPixels() {
  if (paintMaskPixels) {
    return paintMaskPixels;
  }

  const maskCanvas = document.createElement("canvas");
  maskCanvas.width = CANVAS_WIDTH;
  maskCanvas.height = CANVAS_HEIGHT;
  const context = maskCanvas.getContext("2d");
  context.fillStyle = "#fff";
  createPaintableFishPath(context);
  paintMaskPixels = context.getImageData(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT).data;
  return paintMaskPixels;
}

function floodFill(canvas, point, fillColor, tolerance) {
  const context = canvas.getContext("2d", { willReadFrequently: true });
  const path = createPaintableFishPath();
  const startX = Math.max(0, Math.min(CANVAS_WIDTH - 1, Math.floor(point.x)));
  const startY = Math.max(0, Math.min(CANVAS_HEIGHT - 1, Math.floor(point.y)));
  if (!context.isPointInPath(path, startX, startY)) {
    return false;
  }

  const image = context.getImageData(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
  const data = image.data;
  const mask = getPaintMaskPixels();
  const startOffset = (startY * CANVAS_WIDTH + startX) * 4;
  const target = {
    r: data[startOffset],
    g: data[startOffset + 1],
    b: data[startOffset + 2],
    a: data[startOffset + 3],
  };
  const replacement = hexToRgba(fillColor);
  const transparentTarget = isTransparentFillTarget(target);

  const visited = new Uint8Array(CANVAS_WIDTH * CANVAS_HEIGHT);
  const filledPixels = new Uint8Array(CANVAS_WIDTH * CANVAS_HEIGHT);
  const stack = [startY * CANVAS_WIDTH + startX];
  const filled = [];
  let changed = false;

  while (stack.length > 0) {
    const index = stack.pop();
    if (visited[index]) {
      continue;
    }
    visited[index] = 1;

    const offset = index * 4;
    const maskAlpha = mask[offset + 3];
    if (maskAlpha === 0 || !isFillCandidate(data, offset, target, tolerance, transparentTarget)) {
      continue;
    }

    filledPixels[index] = 1;
    filled.push(index);

    const x = index % CANVAS_WIDTH;
    const y = Math.floor(index / CANVAS_WIDTH);
    if (x > 0) stack.push(index - 1);
    if (x < CANVAS_WIDTH - 1) stack.push(index + 1);
    if (y > 0) stack.push(index - CANVAS_WIDTH);
    if (y < CANVAS_HEIGHT - 1) stack.push(index + CANVAS_WIDTH);
  }

  if (filled.length > 0) {
    closeSoftSeams(data, mask, filled, filledPixels);

    for (let i = 0; i < filled.length; i += 1) {
      const offset = filled[i] * 4;
      changed = paintPixel(data, offset, mask[offset + 3], replacement, transparentTarget) || changed;
    }

    if (changed) {
      context.putImageData(image, 0, 0);
    }
  }

  return changed;
}

function ToolButton({ active, children, disabled, label, onClick }) {
  return (
    <button
      aria-label={label}
      className={active ? "icon-button selected" : "icon-button"}
      disabled={disabled}
      onClick={onClick}
      title={label}
      type="button"
    >
      {children}
    </button>
  );
}

export const DrawingCanvas = forwardRef(function DrawingCanvas(
  { brushColor, brushSize, fillTolerance, tool, onColorPick, onDrawingActive, onToolChange },
  ref
) {
  const canvasRef = useRef(null);
  const drawingRef = useRef(false);
  const changedRef = useRef(false);
  const lastPointRef = useRef(null);
  const historyRef = useRef([]);
  const historyIndexRef = useRef(-1);
  const [historyState, setHistoryState] = useState({ canUndo: false, canRedo: false });

  useEffect(() => () => onDrawingActive?.(false), [onDrawingActive]);

  function updateHistoryState() {
    setHistoryState({
      canUndo: historyIndexRef.current > 0,
      canRedo: historyIndexRef.current < historyRef.current.length - 1,
    });
  }

  function pushHistory() {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const context = canvas.getContext("2d", { willReadFrequently: true });
    const snapshot = context.getImageData(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    const activeHistory = historyRef.current.slice(0, historyIndexRef.current + 1);
    activeHistory.push(snapshot);

    if (activeHistory.length > MAX_HISTORY_STEPS) {
      activeHistory.shift();
    }

    historyRef.current = activeHistory;
    historyIndexRef.current = activeHistory.length - 1;
    updateHistoryState();
  }

  function restoreHistory(index) {
    const snapshot = historyRef.current[index];
    if (!snapshot) return;

    const canvas = canvasRef.current;
    const context = canvas.getContext("2d");
    context.putImageData(snapshot, 0, 0);
    historyIndexRef.current = index;
    updateHistoryState();
  }

  useEffect(() => {
    const canvas = canvasRef.current;
    const context = canvas.getContext("2d");
    context.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    context.lineCap = "round";
    context.lineJoin = "round";
    pushHistory();
  }, []);

  useImperativeHandle(ref, () => ({
    clear() {
      const canvas = canvasRef.current;
      const context = canvas.getContext("2d");
      context.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
      pushHistory();
    },
    undo() {
      if (historyIndexRef.current <= 0) return;
      restoreHistory(historyIndexRef.current - 1);
    },
    redo() {
      if (historyIndexRef.current >= historyRef.current.length - 1) return;
      restoreHistory(historyIndexRef.current + 1);
    },
    exportPngBlob() {
      return new Promise((resolve) => {
        const source = canvasRef.current;
        const output = document.createElement("canvas");
        output.width = CANVAS_WIDTH;
        output.height = CANVAS_HEIGHT;
        const context = output.getContext("2d");

        context.save();
        const mask = createPaintableFishPath();
        drawSilhouette(context, FISH_EXPORT_BASE_FILL);
        context.clip(mask);
        context.drawImage(source, 0, 0);
        context.restore();

        output.toBlob((blob) => resolve(blob), "image/png");
      });
    },
  }));

  function drawSegment(point) {
    const canvas = canvasRef.current;
    const context = canvas.getContext("2d");
    const previous = lastPointRef.current ?? point;

    context.save();
    context.clip(createPaintableFishPath());
    context.lineWidth = brushSize;
    context.globalCompositeOperation = tool === "eraser" ? "destination-out" : "source-over";
    context.strokeStyle = brushColor;
    context.fillStyle = brushColor;
    context.beginPath();
    if (previous.x === point.x && previous.y === point.y) {
      context.arc(point.x, point.y, brushSize * 0.5, 0, Math.PI * 2);
      context.fill();
    } else {
      context.moveTo(previous.x, previous.y);
      context.lineTo(point.x, point.y);
      context.stroke();
    }
    context.restore();

    changedRef.current = true;
    lastPointRef.current = point;
  }

  function pickColor(point) {
    const canvas = canvasRef.current;
    const context = canvas.getContext("2d", { willReadFrequently: true });
    const sampleX = Math.max(0, Math.min(CANVAS_WIDTH - 1, Math.floor(point.x)));
    const sampleY = Math.max(0, Math.min(CANVAS_HEIGHT - 1, Math.floor(point.y)));
    const [red, green, blue, alpha] = context.getImageData(sampleX, sampleY, 1, 1).data;

    if (alpha === 0) return;

    onColorPick(toHexColor(red, green, blue));
    onToolChange("brush");
  }

  function startDrawing(event) {
    event.preventDefault();
    event.currentTarget.setPointerCapture?.(event.pointerId);
    const point = getCanvasPoint(canvasRef.current, event);

    if (tool === "eyedropper") {
      pickColor(point);
      return;
    }

    if (tool === "fill") {
      if (floodFill(canvasRef.current, point, brushColor, fillTolerance)) {
        pushHistory();
      }
      return;
    }

    drawingRef.current = true;
    onDrawingActive?.(true);
    changedRef.current = false;
    lastPointRef.current = point;
    drawSegment(point);
  }

  function moveDrawing(event) {
    if (!drawingRef.current) return;
    event.preventDefault();
    drawSegment(getCanvasPoint(canvasRef.current, event));
  }

  function stopDrawing(event) {
    event?.currentTarget?.releasePointerCapture?.(event.pointerId);
    if (drawingRef.current && changedRef.current) {
      pushHistory();
    }

    drawingRef.current = false;
    onDrawingActive?.(false);
    changedRef.current = false;
    lastPointRef.current = null;
  }

  return (
    <section className="drawing-board" aria-label="魚に模様を描く">
      <div className="canvas-stage">
        <canvas
          aria-hidden="true"
          className="silhouette-canvas"
          height={CANVAS_HEIGHT}
          ref={(node) => {
            if (!node) return;
            const context = node.getContext("2d");
            context.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
            drawSilhouette(context);
          }}
          width={CANVAS_WIDTH}
        />
        <canvas
          aria-label="お絵描きキャンバス"
          className={`drawing-canvas tool-${tool}`}
          height={CANVAS_HEIGHT}
          onPointerCancel={stopDrawing}
          onPointerDown={startDrawing}
          onPointerLeave={stopDrawing}
          onPointerMove={moveDrawing}
          onPointerUp={stopDrawing}
          ref={canvasRef}
          width={CANVAS_WIDTH}
        />
        <div className="canvas-toolbar canvas-toolbar-left" role="toolbar" aria-label="描画ツール">
          <div className="tool-group">
            <ToolButton active={tool === "brush"} label="ペン" onClick={() => onToolChange("brush")}>
              <Paintbrush size={19} />
            </ToolButton>
            <ToolButton active={tool === "fill"} label="塗りつぶし" onClick={() => onToolChange("fill")}>
              <PaintBucket size={19} />
            </ToolButton>
            <ToolButton active={tool === "eraser"} label="消しゴム" onClick={() => onToolChange("eraser")}>
              <Eraser size={19} />
            </ToolButton>
            <ToolButton active={tool === "eyedropper"} label="スポイト" onClick={() => onToolChange("eyedropper")}>
              <Pipette size={19} />
            </ToolButton>
          </div>
        </div>
        <div className="canvas-toolbar canvas-toolbar-right" role="toolbar" aria-label="編集ツール">
          <span className="active-tool-swatch" style={{ "--active-tool-color": brushColor }} aria-hidden="true" />
          <div className="tool-group">
            <ToolButton disabled={!historyState.canUndo} label="元に戻す" onClick={() => restoreHistory(historyIndexRef.current - 1)}>
              <Undo2 size={19} />
            </ToolButton>
            <ToolButton disabled={!historyState.canRedo} label="やり直す" onClick={() => restoreHistory(historyIndexRef.current + 1)}>
              <Redo2 size={19} />
            </ToolButton>
          </div>
        </div>
      </div>
    </section>
  );
});
