import React, { useCallback, useMemo, useRef, useState } from "react";
import { Check, Fish, Palette, QrCode, RotateCcw, Send, SlidersHorizontal, Waves, X } from "lucide-react";
import { DrawingCanvas } from "./components/DrawingCanvas";
import { FishSizeSelector } from "./components/FishSizeSelector";
import { QrPanel } from "./components/QrPanel";
import { ReleaseSuccessDialog } from "./components/ReleaseSuccessDialog";
import { brushColors, brushSizeRange } from "./config/fishOptions";
import { fishSizeOptions } from "./config/releaseOptions";
import { uploadFishDrawing } from "./data/fishDrawingStore";
import { useReleaseCooldown } from "./hooks/useReleaseCooldown";
import {
  BLANK_NICKNAME_MESSAGE,
  NG_NICKNAME_MESSAGE,
  normalizeNickname,
  validateNickname,
} from "./validation/nickname";

export function App() {
  const drawingRef = useRef(null);
  const [nickname, setNickname] = useState("");
  const [nicknameTouched, setNicknameTouched] = useState(false);
  const [brushColor, setBrushColor] = useState(brushColors[0].value);
  const [brushSize, setBrushSize] = useState(brushSizeRange.defaultValue);
  const [fishSize, setFishSize] = useState("medium");
  const [tool, setTool] = useState("brush");
  const [status, setStatus] = useState("idle");
  const [message, setMessage] = useState("");
  const [isQrOpen, setIsQrOpen] = useState(false);
  const [isDrawingActive, setIsDrawingActive] = useState(false);
  const [activeToolPanel, setActiveToolPanel] = useState("");
  const [shouldShakeNickname, setShouldShakeNickname] = useState(false);
  const [releasedFish, setReleasedFish] = useState(null);
  const { remainingSeconds, startCooldown } = useReleaseCooldown();

  const nicknameError = useMemo(() => validateNickname(nickname), [nickname]);
  const shouldShowNicknameError =
    nicknameTouched || nicknameError === NG_NICKNAME_MESSAGE || nicknameError === BLANK_NICKNAME_MESSAGE;
  const shownNicknameError = shouldShowNicknameError ? nicknameError : "";
  const isCoolingDown = remainingSeconds > 0;
  const canSubmit = !nicknameError && status !== "sending" && !isCoolingDown;
  const selectedFishSize = fishSizeOptions.find((option) => option.value === fishSize);
  const nicknameInputClass = [
    "nickname-input",
    shownNicknameError ? "invalid" : "",
    shouldShakeNickname ? "shake" : "",
  ]
    .filter(Boolean)
    .join(" ");
  const composerClassName = [
    "composer",
    isDrawingActive ? "is-drawing" : "",
    activeToolPanel ? "has-tools-popover" : "",
  ]
    .filter(Boolean)
    .join(" ");
  const toolsPanelClassName = [
    "panel tools-panel",
    activeToolPanel ? "is-open" : "",
    activeToolPanel ? `section-${activeToolPanel}` : "",
  ]
    .filter(Boolean)
    .join(" ");

  function triggerNicknameShake() {
    setShouldShakeNickname(false);
    window.requestAnimationFrame(() => setShouldShakeNickname(true));
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (isCoolingDown) {
      setStatus("cooldown");
      setMessage(`次の放流まであと${remainingSeconds}秒です。`);
      return;
    }

    const normalized = normalizeNickname(nickname);
    const error = validateNickname(nickname);
    setNicknameTouched(true);
    if (error) {
      setStatus("error");
      setMessage(error);
      triggerNicknameShake();
      return;
    }

    setStatus("sending");
    setMessage("海へ送っています...");

    try {
      const blob = await drawingRef.current.exportPngBlob();
      if (!blob) throw new Error("PNGを書き出せませんでした");

      await uploadFishDrawing({ nickname: normalized, blob, size: fishSize });

      startCooldown();
      setStatus("success");
      setMessage("送信しました。海に新しい魚として追加されます。");
      setReleasedFish({ nickname: normalized, sizeLabel: selectedFishSize?.label ?? "ふつう" });
    } catch (submitError) {
      setStatus("error");
      const detail = submitError?.message ? ` (${submitError.message})` : "";
      setMessage(`送信に失敗しました。Supabase設定か通信を確認してください。${detail}`);
      console.error(submitError);
    }
  }

  function handleColorChange(color) {
    setBrushColor(color);
  }

  function toggleToolPanel(panelName) {
    setActiveToolPanel((currentPanel) => (currentPanel === panelName ? "" : panelName));
  }

  function clearDrawing() {
    drawingRef.current?.clear();
    setActiveToolPanel("");
  }

  function handleNicknameChange(event) {
    const nextValue = event.target.value;
    const nextError = validateNickname(nextValue);

    setNickname(nextValue);
    if (nextError === NG_NICKNAME_MESSAGE || nextError === BLANK_NICKNAME_MESSAGE) {
      setNicknameTouched(true);
      triggerNicknameShake();
    }
  }

  const closeReleaseDialog = useCallback(() => setReleasedFish(null), []);

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

      <form className={composerClassName} onSubmit={handleSubmit}>
        <DrawingCanvas
          brushColor={brushColor}
          brushSize={brushSize}
          onColorPick={handleColorChange}
          onDrawingActive={setIsDrawingActive}
          onToolChange={setTool}
          ref={drawingRef}
          tool={tool}
        />

        <div className="canvas-quick-actions" role="toolbar" aria-label="描画設定">
          <button
            aria-label="色"
            aria-pressed={activeToolPanel === "colors"}
            className={activeToolPanel === "colors" ? "icon-button selected" : "icon-button"}
            onClick={() => toggleToolPanel("colors")}
            title="色"
            type="button"
          >
            <Palette size={19} />
          </button>
          <button
            aria-label="ペンの太さ"
            aria-pressed={activeToolPanel === "brush"}
            className={activeToolPanel === "brush" ? "icon-button selected" : "icon-button"}
            onClick={() => toggleToolPanel("brush")}
            title="ペンの太さ"
            type="button"
          >
            <SlidersHorizontal size={19} />
          </button>
          <button
            aria-label="全部消す"
            className="icon-button"
            onClick={clearDrawing}
            title="全部消す"
            type="button"
          >
            <RotateCcw size={19} />
          </button>
        </div>

        <section className={toolsPanelClassName} aria-label="描画設定">
          <button
            aria-label="閉じる"
            className="icon-button panel-close-button"
            onClick={() => setActiveToolPanel("")}
            title="閉じる"
            type="button"
          >
            <X size={18} />
          </button>
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
                  className={brushColor === color.value ? "swatch selected" : "swatch"}
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

          <button
            aria-label="全部消す"
            className="icon-button clear-button"
            onClick={clearDrawing}
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
            aria-describedby="nickname-hint"
            aria-invalid={shownNicknameError ? "true" : "false"}
            autoComplete="nickname"
            className={nicknameInputClass}
            id="nickname"
            maxLength={12}
            onBlur={() => setNicknameTouched(true)}
            onAnimationEnd={() => setShouldShakeNickname(false)}
            onChange={handleNicknameChange}
            placeholder="例: うみたろう"
            value={nickname}
          />
          <p className={shownNicknameError ? "hint error" : "hint"} id="nickname-hint">
            {shownNicknameError || "1から12文字で入力してね。送信ごとに新しい魚が増えます。"}
          </p>
          <FishSizeSelector onChange={setFishSize} value={fishSize} />
        </section>

        {message ? (
          <p className={`status ${status}`}>
            {status === "success" ? <Check size={18} /> : null}
            {message}
          </p>
        ) : null}

        <button className="release-button" disabled={!canSubmit} type="submit">
          <Send size={20} />
          {status === "sending"
            ? "送信中..."
            : isCoolingDown
              ? `次の放流まであと${remainingSeconds}秒`
              : "海へ送る"}
        </button>
      </form>

      {isQrOpen ? <QrPanel onClose={() => setIsQrOpen(false)} /> : null}
      {releasedFish ? (
        <ReleaseSuccessDialog
          fishSizeLabel={releasedFish.sizeLabel}
          nickname={releasedFish.nickname}
          onClose={closeReleaseDialog}
          remainingSeconds={remainingSeconds}
        />
      ) : null}
    </main>
  );
}
