import React, { forwardRef, useEffect, useImperativeHandle, useRef, useState } from "react";
import { Eraser, PaintBucket, Paintbrush, Pipette, Redo2, Undo2 } from "lucide-react";

const CANVAS_WIDTH = 1024;
const CANVAS_HEIGHT = 512;
const MAX_HISTORY_STEPS = 30;
let fishMaskPixels = null;

function createFishPath(context) {
  const path = new Path2D();

  path.moveTo(932, 252);
  path.bezierCurveTo(920, 190, 846, 145, 749, 134);
  path.bezierCurveTo(720, 102, 676, 82, 618, 84);
  path.bezierCurveTo(558, 86, 512, 119, 504, 152);
  path.bezierCurveTo(478, 134, 438, 128, 404, 146);
  path.bezierCurveTo(376, 160, 350, 187, 320, 202);
  path.bezierCurveTo(294, 216, 266, 216, 238, 222);
  path.bezierCurveTo(224, 238, 217, 256, 220, 275);
  path.bezierCurveTo(254, 284, 288, 286, 314, 304);
  path.bezierCurveTo(364, 342, 440, 368, 554, 379);
  path.bezierCurveTo(684, 393, 824, 372, 894, 316);
  path.bezierCurveTo(920, 294, 933, 273, 932, 252);
  path.closePath();

  path.moveTo(244, 220);
  path.bezierCurveTo(204, 198, 154, 178, 89, 181);
  path.bezierCurveTo(52, 184, 33, 205, 36, 244);
  path.bezierCurveTo(39, 270, 39, 294, 34, 326);
  path.bezierCurveTo(72, 341, 124, 335, 174, 313);
  path.bezierCurveTo(204, 300, 228, 288, 244, 286);
  path.closePath();

  path.moveTo(796, 360);
  path.bezierCurveTo(764, 424, 682, 416, 650, 348);
  path.lineTo(710, 360);
  path.bezierCurveTo(738, 366, 770, 366, 796, 360);
  path.closePath();

  path.moveTo(522, 354);
  path.bezierCurveTo(484, 430, 398, 438, 356, 360);
  path.lineTo(420, 370);
  path.bezierCurveTo(456, 370, 492, 364, 522, 354);
  path.closePath();

  path.moveTo(548, 346);
  path.bezierCurveTo(584, 392, 592, 460, 560, 500);
  path.bezierCurveTo(528, 505, 505, 486, 506, 448);
  path.bezierCurveTo(508, 404, 520, 370, 548, 346);
  path.closePath();

  if (context) {
    context.fill(path);
  }

  return path;
}

function drawSilhouette(context, fillStyle = "rgba(9, 31, 42, 0.86)") {
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

function colorMatches(data, offset, target, tolerance) {
  return Math.abs(data[offset] - target.r) <= tolerance
    && Math.abs(data[offset + 1] - target.g) <= tolerance
    && Math.abs(data[offset + 2] - target.b) <= tolerance
    && Math.abs(data[offset + 3] - target.a) <= tolerance;
}

function getFishMaskPixels() {
  if (fishMaskPixels) {
    return fishMaskPixels;
  }

  const maskCanvas = document.createElement("canvas");
  maskCanvas.width = CANVAS_WIDTH;
  maskCanvas.height = CANVAS_HEIGHT;
  const context = maskCanvas.getContext("2d");
  context.fillStyle = "#fff";
  createFishPath(context);
  fishMaskPixels = context.getImageData(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT).data;
  return fishMaskPixels;
}

function floodFill(canvas, point, fillColor, tolerance) {
  const context = canvas.getContext("2d", { willReadFrequently: true });
  const path = createFishPath();
  const startX = Math.max(0, Math.min(CANVAS_WIDTH - 1, Math.floor(point.x)));
  const startY = Math.max(0, Math.min(CANVAS_HEIGHT - 1, Math.floor(point.y)));
  if (!context.isPointInPath(path, startX, startY)) {
    return false;
  }

  const image = context.getImageData(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
  const data = image.data;
  const mask = getFishMaskPixels();
  const startOffset = (startY * CANVAS_WIDTH + startX) * 4;
  const target = {
    r: data[startOffset],
    g: data[startOffset + 1],
    b: data[startOffset + 2],
    a: data[startOffset + 3],
  };
  const replacement = hexToRgba(fillColor);

  if (colorMatches(data, startOffset, replacement, 0)) {
    return false;
  }

  const visited = new Uint8Array(CANVAS_WIDTH * CANVAS_HEIGHT);
  const stack = [startY * CANVAS_WIDTH + startX];
  let changed = false;

  while (stack.length > 0) {
    const index = stack.pop();
    if (visited[index]) {
      continue;
    }
    visited[index] = 1;

    const offset = index * 4;
    if (mask[offset + 3] === 0 || !colorMatches(data, offset, target, tolerance)) {
      continue;
    }

    data[offset] = replacement.r;
    data[offset + 1] = replacement.g;
    data[offset + 2] = replacement.b;
    data[offset + 3] = replacement.a;
    changed = true;

    const x = index % CANVAS_WIDTH;
    const y = Math.floor(index / CANVAS_WIDTH);
    if (x > 0) stack.push(index - 1);
    if (x < CANVAS_WIDTH - 1) stack.push(index + 1);
    if (y > 0) stack.push(index - CANVAS_WIDTH);
    if (y < CANVAS_HEIGHT - 1) stack.push(index + CANVAS_WIDTH);
  }

  if (changed) {
    context.putImageData(image, 0, 0);
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
  { brushColor, brushSize, fillTolerance, tool, onColorPick, onToolChange },
  ref
) {
  const canvasRef = useRef(null);
  const drawingRef = useRef(false);
  const changedRef = useRef(false);
  const lastPointRef = useRef(null);
  const historyRef = useRef([]);
  const historyIndexRef = useRef(-1);
  const [historyState, setHistoryState] = useState({ canUndo: false, canRedo: false });

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
        const mask = createFishPath();
        drawSilhouette(context, "rgba(255, 255, 255, 0.96)");
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
    context.clip(createFishPath());
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
