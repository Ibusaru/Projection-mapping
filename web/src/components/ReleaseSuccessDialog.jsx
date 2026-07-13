import React, { useEffect, useRef } from "react";
import { Check, Fish, X } from "lucide-react";

export function ReleaseSuccessDialog({ fishSizeLabel, nickname, onClose, remainingSeconds }) {
  const closeButtonRef = useRef(null);

  useEffect(() => {
    closeButtonRef.current?.focus();

    function handleKeyDown(event) {
      if (event.key === "Escape") onClose();
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  return (
    <div aria-modal="true" className="release-modal" onClick={onClose} role="dialog">
      <section
        aria-describedby="release-dialog-description"
        aria-labelledby="release-dialog-title"
        className="panel release-dialog"
        onClick={(event) => event.stopPropagation()}
      >
        <button
          aria-label="閉じる"
          className="icon-button release-dialog-close"
          onClick={onClose}
          ref={closeButtonRef}
          type="button"
        >
          <X size={18} />
        </button>

        <span aria-hidden="true" className="release-dialog-check">
          <Check size={30} strokeWidth={3} />
        </span>
        <p className="release-dialog-eyebrow">放流完了</p>
        <h2 id="release-dialog-title">海へ送りました！</h2>
        <p id="release-dialog-description">
          <strong>{nickname}</strong> の魚が、海の仲間に加わります。
        </p>

        <div className="release-result-size">
          <Fish aria-hidden="true" size={24} />
          <span>魚の大きさ</span>
          <strong>{fishSizeLabel}</strong>
        </div>

        <p className="release-dialog-cooldown" aria-live="polite">
          {remainingSeconds > 0 ? (
            <>
              次の放流まで <strong>{remainingSeconds}秒</strong>
            </>
          ) : (
            <strong>次の魚を放流できます</strong>
          )}
        </p>

        <button className="release-dialog-action" onClick={onClose} type="button">
          描画画面に戻る
        </button>
      </section>
    </div>
  );
}
