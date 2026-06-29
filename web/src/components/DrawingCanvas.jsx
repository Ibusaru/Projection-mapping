import React, { forwardRef, useEffect, useImperativeHandle, useRef } from "react";
import { Eraser, Paintbrush } from "lucide-react";

const CANVAS_WIDTH = 1024;
const CANVAS_HEIGHT = 512;

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

export const DrawingCanvas = forwardRef(function DrawingCanvas(
  { brushColor, brushSize, tool, onToolChange },
  ref
) {
  const canvasRef = useRef(null);
  const drawingRef = useRef(false);
  const lastPointRef = useRef(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const context = canvas.getContext("2d");
    context.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    context.lineCap = "round";
    context.lineJoin = "round";
  }, []);

  useImperativeHandle(ref, () => ({
    clear() {
      const canvas = canvasRef.current;
      const context = canvas.getContext("2d");
      context.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
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

    lastPointRef.current = point;
  }

  function startDrawing(event) {
    event.preventDefault();
    event.currentTarget.setPointerCapture?.(event.pointerId);
    const point = getCanvasPoint(canvasRef.current, event);
    drawingRef.current = true;
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
    drawingRef.current = false;
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
      </div>
    </section>
  );
});
