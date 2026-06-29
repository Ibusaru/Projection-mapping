import React, { useMemo, useRef, useState } from "react";
import { Check, Fish, QrCode, RotateCcw, Send, Waves } from "lucide-react";
import { DrawingCanvas } from "./components/DrawingCanvas";
import { QrPanel } from "./components/QrPanel";
import { brushColors, brushSizes } from "./config/fishOptions";
import { uploadFishDrawing } from "./data/fishDrawingStore";
import { normalizeNickname, validateNickname } from "./validation/nickname";

export function App() {
  const drawingRef = useRef(null);
  const [nickname, setNickname] = useState("");
  const [brushColor, setBrushColor] = useState(brushColors[0].value);
  const [brushSize, setBrushSize] = useState(brushSizes[1].value);
  const [tool, setTool] = useState("brush");
  const [status, setStatus] = useState("idle");
  const [message, setMessage] = useState("");
  const [isQrOpen, setIsQrOpen] = useState(false);

  const nicknameError = useMemo(() => validateNickname(nickname), [nickname]);
  const canSubmit = !nicknameError && status !== "sending";

  async function handleSubmit(event) {
    event.preventDefault();
    const normalized = normalizeNickname(nickname);
    const error = validateNickname(normalized);
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
          onToolChange={setTool}
          ref={drawingRef}
          tool={tool}
        />

        <section className="panel name-panel">
          <label htmlFor="nickname">
            <Fish size={18} />
            ニックネーム
          </label>
          <input
            autoComplete="nickname"
            id="nickname"
            maxLength={12}
            onChange={(event) => setNickname(event.target.value)}
            placeholder="例: うみたろう"
            value={nickname}
          />
          <p className={nicknameError ? "hint error" : "hint"}>
            {nicknameError || "1から12文字で入力してね。送信ごとに新しい魚が増えます。"}
          </p>
        </section>

        <section className="panel tools-panel" aria-label="色と太さ">
          <div className="swatches">
            {brushColors.map((color) => (
              <button
                aria-label={color.name}
                className={brushColor === color.value && tool === "brush" ? "swatch selected" : "swatch"}
                key={color.value}
                onClick={() => {
                  setBrushColor(color.value);
                  setTool("brush");
                }}
                style={{ "--swatch": color.value }}
                title={color.name}
                type="button"
              />
            ))}
          </div>

          <div className="size-picker">
            {brushSizes.map((size) => (
              <button
                className={brushSize === size.value ? "size-button selected" : "size-button"}
                key={size.value}
                onClick={() => setBrushSize(size.value)}
                type="button"
              >
                {size.label}
              </button>
            ))}
          </div>

          <button
            aria-label="全部消す"
            className="icon-button"
            onClick={() => drawingRef.current.clear()}
            title="全部消す"
            type="button"
          >
            <RotateCcw size={19} />
          </button>
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
