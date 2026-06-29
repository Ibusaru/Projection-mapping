import React, { useMemo, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { createClient } from "@supabase/supabase-js";
import { Check, Fish, QrCode, RotateCcw, Send, Waves } from "lucide-react";
import { DrawingCanvas } from "./components/DrawingCanvas";
import { QrPanel } from "./components/QrPanel";
import { blockedWords, brushColors, brushSizes, defaultFishPayload } from "./config/fishOptions";
import "./styles.css";

const storageBucket = "fish-drawings";
const supabaseUrl = import.meta.env.VITE_SUPABASE_URL;
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY;
const supabase =
  supabaseUrl && supabaseAnonKey ? createClient(supabaseUrl, supabaseAnonKey) : null;

function normalizeNickname(value) {
  return value.replace(/\s+/g, "").trim();
}

function sanitizePathPart(value) {
  return encodeURIComponent(value)
    .replace(/%/g, "")
    .replace(/[^a-zA-Z0-9_-]/g, "")
    .slice(0, 48);
}

function validateNickname(value) {
  const nickname = normalizeNickname(value);
  if (nickname.length < 1) return "ニックネームを入力してね";
  if (nickname.length > 12) return "12文字以内にしてね";
  if (/[\r\n]/.test(value)) return "改行は使えません";
  if (/[<>]/.test(value)) return "使えない記号があります";
  if (/^[\p{P}\p{S}]+$/u.test(nickname)) return "文字を1つ以上入れてね";

  const lower = nickname.toLowerCase();
  if (blockedWords.some((word) => lower.includes(word.toLowerCase()))) {
    return "別のニックネームにしてね";
  }

  return "";
}

async function uploadFishDrawing({ nickname, blob }) {
  if (!supabase) {
    const localFish = JSON.parse(localStorage.getItem("local_fishes") ?? "[]");
    const publicUrl = URL.createObjectURL(blob);
    const nextFish = {
      ...defaultFishPayload,
      id: crypto.randomUUID(),
      nickname,
      texture_path: `local/${sanitizePathPart(nickname)}.png`,
      texture_url: publicUrl,
      created_at: new Date().toISOString(),
      updated_at: new Date().toISOString(),
    };
    const withoutOld = localFish.filter((fish) => fish.nickname !== nickname);
    localStorage.setItem("local_fishes", JSON.stringify([...withoutOld, nextFish]));
    return;
  }

  const { data: previousFish, error: selectError } = await supabase
    .from("fishes")
    .select("id, texture_path")
    .eq("nickname", nickname)
    .maybeSingle();

  if (selectError) throw selectError;

  const safeName = sanitizePathPart(nickname) || crypto.randomUUID();
  const timestamp = Date.now();
  const texturePath = `${safeName}/${timestamp}.png`;

  const { error: uploadError } = await supabase.storage
    .from(storageBucket)
    .upload(texturePath, blob, {
      cacheControl: "60",
      contentType: "image/png",
      upsert: false,
    });

  if (uploadError) throw uploadError;

  const { data: publicData } = supabase.storage.from(storageBucket).getPublicUrl(texturePath);
  const textureUrl = publicData.publicUrl;
  const now = new Date().toISOString();

  const payload = {
    ...defaultFishPayload,
    nickname,
    texture_path: texturePath,
    texture_url: textureUrl,
    updated_at: now,
  };

  const { error: upsertError } = await supabase
    .from("fishes")
    .upsert(payload, { onConflict: "nickname" });

  if (upsertError) throw upsertError;

  if (previousFish?.texture_path && previousFish.texture_path !== texturePath) {
    const { error: removeError } = await supabase.storage
      .from(storageBucket)
      .remove([previousFish.texture_path]);

    if (removeError) {
      console.warn("古い画像の削除に失敗しました", removeError);
    }
  }
}

function App() {
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
      setMessage("送信しました。同じ名前の魚がいれば新しい絵に更新されます。");
    } catch (submitError) {
      setStatus("error");
      setMessage("送信に失敗しました。Supabase設定か通信を確認してください。");
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
            {nicknameError || "同じ名前で送ると、前の魚の画像を新しい絵に更新します。"}
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

createRoot(document.getElementById("root")).render(<App />);
