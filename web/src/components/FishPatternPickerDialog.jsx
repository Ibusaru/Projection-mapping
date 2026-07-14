import React, { useEffect, useRef, useState } from "react";
import { LayoutGrid, RotateCcw, X } from "lucide-react";
import {
  fishPatternCategories,
  fishPatternOptions,
  getFishPatternOption,
} from "../config/fishPatternGuides";
import { FishPatternThumbnail } from "./FishPatternGuideCanvas";

const freePattern = fishPatternOptions.find((option) => option.id === "none");

function PatternChoice({ activePatternId, option, onChoose }) {
  const isSelected = option.id === activePatternId;

  return (
    <button
      aria-label={`${option.label}：${option.description}`}
      aria-pressed={isSelected}
      className={isSelected ? "pattern-option is-selected" : "pattern-option"}
      onClick={() => onChoose(option.id)}
      type="button"
    >
      <span className="pattern-option-preview">
        <FishPatternThumbnail patternId={option.id} />
      </span>
      <span className="pattern-option-copy">
        <strong>{option.label}</strong>
        <small>{option.description}</small>
      </span>
    </button>
  );
}

export function FishPatternPickerDialog({
  activePatternId,
  hasDrawing,
  onChange,
  onClose,
}) {
  const closeButtonRef = useRef(null);
  const confirmButtonRef = useRef(null);
  const [pendingPatternId, setPendingPatternId] = useState(null);
  const pendingPattern = pendingPatternId ? getFishPatternOption(pendingPatternId) : null;

  useEffect(() => {
    if (pendingPatternId) confirmButtonRef.current?.focus();
    else closeButtonRef.current?.focus();
  }, [pendingPatternId]);

  useEffect(() => {
    function handleKeyDown(event) {
      if (event.key !== "Escape") return;
      if (pendingPatternId) setPendingPatternId(null);
      else onClose();
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose, pendingPatternId]);

  function applyPattern(patternId, resetDrawing) {
    onChange(patternId, { resetDrawing });
    onClose();
  }

  function choosePattern(patternId) {
    if (patternId === activePatternId) {
      onClose();
      return;
    }

    if (hasDrawing) {
      setPendingPatternId(patternId);
      return;
    }

    applyPattern(patternId, false);
  }

  return (
    <div className="pattern-modal" onClick={onClose}>
      <section
        aria-describedby={pendingPattern ? "pattern-change-description" : "pattern-picker-description"}
        aria-labelledby={pendingPattern ? "pattern-change-title" : "pattern-picker-title"}
        aria-modal="true"
        className="panel pattern-dialog"
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

        {pendingPattern ? (
          <div className="pattern-confirmation">
            <span aria-hidden="true" className="pattern-dialog-icon warning">
              <RotateCcw size={27} />
            </span>
            <p className="pattern-dialog-eyebrow">下絵の変更</p>
            <h2 id="pattern-change-title">描いたものを消して変更しますか？</h2>
            <p id="pattern-change-description">
              下絵を「{pendingPattern.label}」に変えると、今の絵と元に戻す履歴が消えます。
            </p>
            <div className="pattern-confirm-preview">
              <FishPatternThumbnail patternId={pendingPattern.id} />
              <strong>{pendingPattern.label}</strong>
            </div>
            <div className="pattern-confirm-actions">
              <button className="pattern-cancel-button" onClick={() => setPendingPatternId(null)} type="button">
                模様選びに戻る
              </button>
              <button
                className="pattern-confirm-button"
                onClick={() => applyPattern(pendingPattern.id, true)}
                ref={confirmButtonRef}
                type="button"
              >
                <RotateCcw size={18} />
                消して変更
              </button>
            </div>
          </div>
        ) : (
          <>
            <header className="pattern-dialog-header">
              <span aria-hidden="true" className="pattern-dialog-icon">
                <LayoutGrid size={27} />
              </span>
              <div>
                <p className="pattern-dialog-eyebrow">お絵描きのヒント</p>
                <h2 id="pattern-picker-title">下絵を選ぶ</h2>
              </div>
            </header>
            <p id="pattern-picker-description" className="pattern-dialog-description">
              薄い線は送信されません。バケツを使うと区画ごとに塗れます。
            </p>

            <div className="pattern-free-choice">
              <PatternChoice
                activePatternId={activePatternId}
                onChoose={choosePattern}
                option={freePattern}
              />
            </div>

            <div className="pattern-categories">
              {fishPatternCategories.map((category) => (
                <section className="pattern-category" key={category.id}>
                  <h3>{category.label}</h3>
                  <div className="pattern-grid">
                    {fishPatternOptions
                      .filter((option) => option.category === category.id)
                      .map((option) => (
                        <PatternChoice
                          activePatternId={activePatternId}
                          key={option.id}
                          onChoose={choosePattern}
                          option={option}
                        />
                      ))}
                  </div>
                </section>
              ))}
            </div>
          </>
        )}
      </section>
    </div>
  );
}
