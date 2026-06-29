import React, { useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { createClient } from "@supabase/supabase-js";
import { Check, Fish, QrCode, Send, Sparkles, Waves } from "lucide-react";
import { FishPreview } from "./components/FishPreview";
import { ColorGrid, SegmentedControl } from "./components/FormControls";
import { QrPanel } from "./components/QrPanel";
import {
  blockedWords,
  colorOptions,
  patternOptions,
  personalityOptions,
  sizeOptions,
  speciesOptions,
  subColorOptions,
} from "./config/fishOptions";
import "./styles.css";

const supabaseUrl = import.meta.env.VITE_SUPABASE_URL;
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY;
const supabase =
  supabaseUrl && supabaseAnonKey ? createClient(supabaseUrl, supabaseAnonKey) : null;

function normalizeNickname(value) {
  return value.replace(/\s+/g, "").trim();
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

function App() {
  const [nickname, setNickname] = useState("");
  const [species, setSpecies] = useState("clownfish");
  const [mainColor, setMainColor] = useState("#ff6b4a");
  const [subColor, setSubColor] = useState("#ffffff");
  const [pattern, setPattern] = useState("stripe");
  const [size, setSize] = useState("medium");
  const [personality, setPersonality] = useState("schooling");
  const [status, setStatus] = useState("idle");
  const [message, setMessage] = useState("");
  const [isQrOpen, setIsQrOpen] = useState(false);

  const nicknameError = useMemo(() => validateNickname(nickname), [nickname]);
  const selectedSpecies = speciesOptions.find((item) => item.id === species);
  const canSubmit = !nicknameError && status !== "sending";

  async function handleSubmit(event) {
    event.preventDefault();
    const error = validateNickname(nickname);
    if (error) {
      setMessage(error);
      setStatus("error");
      return;
    }

    const payload = {
      nickname: normalizeNickname(nickname),
      species,
      main_color: mainColor,
      sub_color: subColor,
      pattern,
      size,
      personality,
    };

    setStatus("sending");
    setMessage("海へ向かっています...");

    try {
      if (!supabase) {
        const localFish = JSON.parse(localStorage.getItem("local_fishes") ?? "[]");
        localFish.push({ ...payload, id: crypto.randomUUID(), created_at: new Date().toISOString() });
        localStorage.setItem("local_fishes", JSON.stringify(localFish));
      } else {
        const { error: insertError } = await supabase.from("fishes").insert(payload);
        if (insertError) throw insertError;
      }

      setStatus("success");
      setMessage("放流しました。少し待つと大きな海に現れます。");
    } catch (submitError) {
      setStatus("error");
      setMessage("放流に失敗しました。近くのスタッフに知らせてください。");
      console.error(submitError);
    }
  }

  return (
    <main className="app-shell">
      <section className="hero">
        <div>
          <p className="kicker">
            <Waves size={18} />
            みんなでつくる海
          </p>
          <h1>魚をつくって海へ放流</h1>
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
      </section>

      <form className="composer" onSubmit={handleSubmit}>
        <FishPreview
          mainColor={mainColor}
          pattern={pattern}
          size={size}
          species={species}
          subColor={subColor}
        />

        <section className="field-block">
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
            {nicknameError || "近づいたときだけ海の中で表示されます"}
          </p>
        </section>

        <section className="field-block">
          <h2>魚の種類</h2>
          <div className="species-list">
            {speciesOptions.map((option) => (
              <button
                className={species === option.id ? "selected" : ""}
                key={option.id}
                onClick={() => setSpecies(option.id)}
                type="button"
              >
                <span>{option.label}</span>
                <small>{option.description}</small>
              </button>
            ))}
          </div>
        </section>

        <ColorGrid label="メインカラー" onChange={setMainColor} options={colorOptions} value={mainColor} />
        <ColorGrid label="サブカラー" onChange={setSubColor} options={subColorOptions} value={subColor} />

        <SegmentedControl label="模様" onChange={setPattern} options={patternOptions} value={pattern} />
        <SegmentedControl label="サイズ" onChange={setSize} options={sizeOptions} value={size} />
        <SegmentedControl label="性格" onChange={setPersonality} options={personalityOptions} value={personality} />

        <div className="summary">
          <Sparkles size={18} />
          <span>
            {selectedSpecies?.label} / {patternOptions.find((item) => item.id === pattern)?.label} /{" "}
            {personalityOptions.find((item) => item.id === personality)?.label}
          </span>
        </div>

        {message ? (
          <p className={`status ${status}`}>
            {status === "success" ? <Check size={18} /> : null}
            {message}
          </p>
        ) : null}

        <button className="release-button" disabled={!canSubmit} type="submit">
          <Send size={20} />
          {status === "sending" ? "放流中..." : "海へ放流"}
        </button>
      </form>

      {isQrOpen ? <QrPanel onClose={() => setIsQrOpen(false)} /> : null}
    </main>
  );
}

createRoot(document.getElementById("root")).render(<App />);
