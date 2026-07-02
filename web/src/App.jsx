import React, { useMemo, useRef, useState } from "react";
import { Check, Fish, Gauge, Palette, QrCode, RotateCcw, Send, Waves } from "lucide-react";
import { DrawingCanvas } from "./components/DrawingCanvas";
import { QrPanel } from "./components/QrPanel";
import { brushColors, brushSizeRange, fillToleranceRange } from "./config/fishOptions";
import { uploadFishDrawing } from "./data/fishDrawingStore";
import { normalizeNickname, validateNickname } from "./validation/nickname";

export function App() {
  const drawingRef = useRef(null);
  const [nickname, setNickname] = useState("");
  const [nicknameTouched, setNicknameTouched] = useState(false);
  const [brushColor, setBrushColor] = useState(brushColors[0].value);
  const [brushSize, setBrushSize] = useState(brushSizeRange.defaultValue);
  const [fillTolerance, setFillTolerance] = useState(fillToleranceRange.defaultValue);
  const [tool, setTool] = useState("brush");
  const [status, setStatus] = useState("idle");
  const [message, setMessage] = useState("");
  const [isQrOpen, setIsQrOpen] = useState(false);

  const nicknameError = useMemo(() => validateNickname(nickname), [nickname]);
  const shownNicknameError = nicknameTouched ? nicknameError : "";
  const canSubmit = !nicknameError && status !== "sending";

  async function handleSubmit(event) {
    event.preventDefault();
    const normalized = normalizeNickname(nickname);
    const error = validateNickname(normalized);
    setNicknameTouched(true);
    if (error) {
      setStatus("error");
      setMessage(error);
      return;
    }

    setStatus("sending");
    setMessage("海へ送っています...");

    try {
      const blob = await drawingRef.current.exportPngBlob();
      if (!blob) throw new Error("PNGを書き出せませんでした");

      await uploadFishDrawing({ nickname: normalized, blob });

      setStatus("success");
      setMessage("送信しました。海に新しい魚として追加されます。");
    } catch (submitError) {
      setStatus("error");
      const detail = submitError?.message ? ` (${submitError.message})` : "";
      setMessage(`送信に失敗しました。Supabase設定か通信を確認してください。${detail}`);
      console.error(submitError);
    }
  }

  function handleColorChange(color) {
    setBrushColor(color);
    setTool("brush");
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="kicker">
            <Waves size={17} />
            みんなでつくる海
          </p>
          <h1>魚に模様を描く</h1>
        </div>
        <button
          aria-expanded={isQrOpen}
          aria-haspopup="dialog"
          className="qr-trigger"
          onClick={() => setIsQrOpen(true)}
          type="button"
        >
          <QrCode size={18} />
          QR
        </button>
      </header>

      <form className="composer" onSubmit={handleSubmit}>
        <DrawingCanvas
          brushColor={brushColor}
          brushSize={brushSize}
          fillTolerance={fillTolerance}
          onColorPick={handleColorChange}
          onToolChange={setTool}
          ref={drawingRef}
          tool={tool}
        />

        <section className="panel tools-panel" aria-label="描画設定">
          <div className="tool-section color-section">
            <div className="tool-section-title">
              <Palette size={18} />
              <span>色</span>
              <span className="current-color" style={{ "--current-color": brushColor }} />
            </div>
            <div className="swatches">
              {brushColors.map((color) => (
                <button
                  aria-label={color.name}
                  className={brushColor === color.value && tool === "brush" ? "swatch selected" : "swatch"}
                  key={color.value}
                  onClick={() => handleColorChange(color.value)}
                  style={{ "--swatch": color.value }}
                  title={color.name}
                  type="button"
                />
              ))}
              <label className="custom-color" title="自由な色">
                <input
                  aria-label="自由な色"
                  onChange={(event) => handleColorChange(event.target.value)}
                  type="color"
                  value={brushColor}
                />
              </label>
            </div>
          </div>

          <div className="tool-section size-section">
            <label className="tool-section-title" htmlFor="brush-size">
              <span>太さ</span>
              <span className="size-value">{brushSize}px</span>
            </label>
            <div className="size-control">
              <input
                aria-label="ペンの太さ"
                id="brush-size"
                max={brushSizeRange.max}
                min={brushSizeRange.min}
                onChange={(event) => setBrushSize(Number(event.target.value))}
                step="1"
                type="range"
                value={brushSize}
              />
              <span
                aria-hidden="true"
                className="brush-preview"
                style={{ "--brush-size": `${brushSize}px`, "--brush-color": brushColor }}
              />
            </div>
          </div>

          <div className="tool-section fill-section">
            <label className="tool-section-title" htmlFor="fill-tolerance">
              <Gauge size={18} />
              <span>塗り範囲</span>
              <span className="size-value">{fillTolerance}</span>
            </label>
            <input
              aria-label="塗りつぶしの範囲"
              id="fill-tolerance"
              max={fillToleranceRange.max}
              min={fillToleranceRange.min}
              onChange={(event) => setFillTolerance(Number(event.target.value))}
              step="1"
              type="range"
              value={fillTolerance}
            />
          </div>

          <button
            aria-label="全部消す"
            className="icon-button clear-button"
            onClick={() => drawingRef.current.clear()}
            title="全部消す"
            type="button"
          >
            <RotateCcw size={19} />
          </button>
        </section>

        <section className="panel name-panel">
          <label htmlFor="nickname">
            <Fish size={18} />
            ニックネーム
          </label>
          <input
            autoComplete="nickname"
            id="nickname"
            maxLength={12}
            onBlur={() => setNicknameTouched(true)}
            onChange={(event) => setNickname(event.target.value)}
            placeholder="例: うみたろう"
            value={nickname}
          />
          <p className={shownNicknameError ? "hint error" : "hint"}>
            {shownNicknameError || "1から12文字で入力してね。送信ごとに新しい魚が増えます。"}
          </p>
        </section>

        {message ? (
          <p className={`status ${status}`}>
            {status === "success" ? <Check size={18} /> : null}
            {message}
          </p>
        ) : null}

        <button className="release-button" disabled={!canSubmit} type="submit">
          <Send size={20} />
          {status === "sending" ? "送信中..." : "海へ送る"}
        </button>
      </form>

      {isQrOpen ? <QrPanel onClose={() => setIsQrOpen(false)} /> : null}
    </main>
  );
}
