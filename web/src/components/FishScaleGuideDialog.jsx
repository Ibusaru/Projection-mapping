import React, { useEffect, useRef } from "react";
import { CheckCircle2, Layers3, X } from "lucide-react";
import { fishPatternOptions } from "../config/fishPatternGuides";
import { FishPatternThumbnail } from "./FishPatternGuideCanvas";

function ScaleGuideChoice({ activePatternId, onChoose, option }) {
  const isSelected = option.id === activePatternId;

  return (
    <button
      aria-label={`${option.title}：${option.description}`}
      aria-pressed={isSelected}
      className={isSelected ? "pattern-option is-selected" : "pattern-option"}
      onClick={() => onChoose(option.id)}
      type="button"
    >
      <span className="pattern-option-preview">
        <FishPatternThumbnail patternId={option.id} />
      </span>
      <span className="pattern-option-copy">
        <span className="pattern-option-heading">
          <strong>{option.title}</strong>
          <span className={option.id === "scales" ? "scale-state is-on" : "scale-state"}>
            {option.label}
          </span>
        </span>
        <small>{option.description}</small>
      </span>
    </button>
  );
}

export function FishScaleGuideDialog({ activePatternId, onClose, onSelect }) {
  const closeButtonRef = useRef(null);

  useEffect(() => {
    closeButtonRef.current?.focus();
  }, []);

  useEffect(() => {
    function handleKeyDown(event) {
      if (event.key === "Escape") onClose();
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  function chooseGuide(patternId) {
    onSelect(patternId);
    onClose();
  }

  return (
    <div className="pattern-modal" onClick={onClose}>
      <section
        aria-describedby="scale-guide-description"
        aria-labelledby="scale-guide-title"
        aria-modal="true"
        className="panel pattern-dialog is-initial scale-guide-dialog"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <button
          aria-label="閉じる"
          className="icon-button pattern-dialog-close"
          onClick={onClose}
          ref={closeButtonRef}
          title="閉じる"
          type="button"
        >
          <X size={18} />
        </button>

        <header className="pattern-dialog-header">
          <span aria-hidden="true" className="pattern-dialog-icon">
            <Layers3 size={27} />
          </span>
          <div>
            <p className="pattern-dialog-eyebrow">お絵描きのスタート</p>
            <h2 id="scale-guide-title">模様を表示しますか？</h2>
          </div>
        </header>
        <p id="scale-guide-description" className="pattern-dialog-description">
          模様OFFでは目も含めてガイドを消します。あとから切り替えても、描いた絵と履歴は残ります。
        </p>
        <p className="pattern-preserve-note">
          <CheckCircle2 aria-hidden="true" size={16} />
          模様ONでは丸い目もそのまま塗りつぶせます
        </p>

        <div className="scale-guide-grid">
          {fishPatternOptions.map((option) => (
            <ScaleGuideChoice
              activePatternId={activePatternId}
              key={option.id}
              onChoose={chooseGuide}
              option={option}
            />
          ))}
        </div>
      </section>
    </div>
  );
}
