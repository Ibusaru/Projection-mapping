import React, { useEffect, useRef } from "react";
import { Trash2, X } from "lucide-react";

export function AdminDeleteDialog({ fish, isDeleting, onCancel, onConfirm }) {
  const cancelButtonRef = useRef(null);

  useEffect(() => {
    cancelButtonRef.current?.focus();

    function handleKeyDown(event) {
      if (event.key === "Escape" && !isDeleting) onCancel();
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isDeleting, onCancel]);

  return (
    <div
      aria-modal="true"
      className="admin-delete-modal"
      onClick={isDeleting ? undefined : onCancel}
      role="dialog"
    >
      <section
        aria-describedby="admin-delete-description"
        aria-labelledby="admin-delete-title"
        className="admin-delete-dialog"
        onClick={(event) => event.stopPropagation()}
      >
        <button
          aria-label="閉じる"
          className="admin-delete-close"
          disabled={isDeleting}
          onClick={onCancel}
          type="button"
        >
          <X size={18} />
        </button>

        <span aria-hidden="true" className="admin-delete-icon">
          <Trash2 size={26} />
        </span>
        <p className="admin-eyebrow">DELETE FISH</p>
        <h2 id="admin-delete-title">この魚を削除しますか？</h2>
        <div className="admin-delete-fish-summary">
          <span className="admin-fish-thumb">
            {fish.texture_url ? <img alt="" src={fish.texture_url} /> : <Trash2 size={22} />}
          </span>
          <strong>{fish.nickname}</strong>
        </div>
        <p className="admin-delete-description" id="admin-delete-description">
          削除した魚は元に戻せません。
        </p>

        <div className="admin-delete-actions">
          <button
            className="admin-delete-cancel"
            disabled={isDeleting}
            onClick={onCancel}
            ref={cancelButtonRef}
            type="button"
          >
            キャンセル
          </button>
          <button className="admin-delete-confirm" disabled={isDeleting} onClick={onConfirm} type="button">
            <Trash2 size={17} />
            {isDeleting ? "削除中…" : "削除する"}
          </button>
        </div>
      </section>
    </div>
  );
}
