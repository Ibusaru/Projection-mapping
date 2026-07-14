import React, { useEffect, useRef } from "react";
import { Fish, Send, X } from "lucide-react";
import { FishSizeSelector } from "./FishSizeSelector";

export function ReleaseSettingsDialog({
  canSubmit,
  fishSize,
  message,
  nickname,
  nicknameInputClass,
  onAnimationEnd,
  onClose,
  onNicknameBlur,
  onNicknameChange,
  onSizeChange,
  onSubmit,
  status,
  nicknameHint,
}) {
  const inputRef = useRef(null);

  useEffect(() => {
    inputRef.current?.focus();

    function handleKeyDown(event) {
      if (event.key === "Escape") onClose();
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  return (
    <div aria-modal="true" className="release-modal" onClick={onClose} role="dialog">
      <form
        aria-describedby="release-settings-description"
        aria-labelledby="release-settings-title"
        className="panel release-dialog release-settings-dialog"
        onClick={(event) => event.stopPropagation()}
        onSubmit={onSubmit}
      >
        <button aria-label="閉じる" className="icon-button release-dialog-close" onClick={onClose} type="button">
          <X size={18} />
        </button>

        <span aria-hidden="true" className="release-settings-icon">
          <Fish size={28} />
        </span>
        <p className="release-dialog-eyebrow">放流の設定</p>
        <h2 id="release-settings-title">魚に名前をつけよう</h2>
        <p id="release-settings-description">ニックネームと魚の大きさを設定してから放流します。</p>

        <div className="release-settings-fields">
          <label className="release-settings-label" htmlFor="nickname">
            ニックネーム
          </label>
          <input
            aria-describedby="release-nickname-hint"
            aria-invalid={nicknameHint ? "true" : "false"}
            autoComplete="nickname"
            className={nicknameInputClass}
            id="nickname"
            maxLength={12}
            onAnimationEnd={onAnimationEnd}
            onBlur={onNicknameBlur}
            onChange={onNicknameChange}
            placeholder="魚の名前を入力"
            ref={inputRef}
            value={nickname}
          />
          <p className={nicknameHint ? "hint error" : "hint"} id="release-nickname-hint">
            {nicknameHint || "1〜12文字で入力してください"}
          </p>
          <FishSizeSelector onChange={onSizeChange} value={fishSize} />
        </div>

        {message && status !== "success" ? <p className={`status ${status}`}>{message}</p> : null}

        <div className="release-settings-actions">
          <button className="release-settings-cancel" onClick={onClose} type="button">
            キャンセル
          </button>
          <button className="release-dialog-action" disabled={!canSubmit} type="submit">
            <Send size={18} />
            海へ放流
          </button>
        </div>
      </form>
    </div>
  );
}
