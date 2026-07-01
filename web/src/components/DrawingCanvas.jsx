import React, { forwardRef, useEffect, useImperativeHandle, useRef, useState } from "react";
import { Eraser, Paintbrush, Pipette, Redo2, Undo2 } from "lucide-react";

const CANVAS_WIDTH = 1024;
const CANVAS_HEIGHT = 512;
const MAX_HISTORY_STEPS = 30;

function createFishPath(context) {
  const path = new Path2D();
  path.ellipse(470, 256, 315, 150, 0, 0, Math.PI * 2);
  path.moveTo(760, 256);
  path.lineTo(975, 98);
  path.quadraticCurveTo(908, 256, 975, 414);
  path.closePath();
  path.moveTo(380, 130);
  path.quadraticCurveTo(480, 24, 580, 138);
  path.lineTo(500, 165);
  path.closePath();
  path.moveTo(390, 384);
  path.quadraticCurveTo(500, 486, 610, 374);
  path.lineTo(520, 350);
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

export const DrawingCanvas = forwardRef(function DrawingCanvas(
  { brushColor, brushSize, tool, onColorPick, onToolChange },
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
    context.beginPath();
    if (previous.x === point.x && previous.y === point.y) {
      context.arc(point.x, point.y, brushSize * 0.5, 0, Math.PI * 2);
      context.fillStyle = brushColor;
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
          aria-label="お絵かきキャンバス"
          className="drawing-canvas"
          height={CANVAS_HEIGHT}
          onPointerCancel={stopDrawing}
          onPointerDown={startDrawing}
          onPointerLeave={stopDrawing}
          onPointerMove={moveDrawing}
          onPointerUp={stopDrawing}
          ref={canvasRef}
          width={CANVAS_WIDTH}
        />
      </div>

      <div className="tool-row" role="toolbar" aria-label="描画ツール">
        <button
          aria-label="ペン"
          className={tool === "brush" ? "icon-button selected" : "icon-button"}
          onClick={() => onToolChange("brush")}
          title="ペン"
          type="button"
        >
          <Paintbrush size={19} />
        </button>
        <button
          aria-label="消しゴム"
          className={tool === "eraser" ? "icon-button selected" : "icon-button"}
          onClick={() => onToolChange("eraser")}
          title="消しゴム"
          type="button"
        >
          <Eraser size={19} />
        </button>
        <button
          aria-label="スポイト"
          className={tool === "eyedropper" ? "icon-button selected" : "icon-button"}
          onClick={() => onToolChange("eyedropper")}
          title="スポイト"
          type="button"
        >
          <Pipette size={19} />
        </button>
        <span className="tool-divider" aria-hidden="true" />
        <button
          aria-label="元に戻す"
          className="icon-button"
          disabled={!historyState.canUndo}
          onClick={() => restoreHistory(historyIndexRef.current - 1)}
          title="元に戻す"
          type="button"
        >
          <Undo2 size={19} />
        </button>
        <button
          aria-label="やり直し"
          className="icon-button"
          disabled={!historyState.canRedo}
          onClick={() => restoreHistory(historyIndexRef.current + 1)}
          title="やり直し"
          type="button"
        >
          <Redo2 size={19} />
        </button>
      </div>
    </section>
  );
});
