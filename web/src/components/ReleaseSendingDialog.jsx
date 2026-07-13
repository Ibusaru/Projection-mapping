import React, { useEffect, useRef } from "react";
import { LoaderCircle } from "lucide-react";

export function ReleaseSendingDialog() {
  const dialogRef = useRef(null);

  useEffect(() => {
    dialogRef.current?.focus();

    function preventDismiss(event) {
      if (event.key === "Escape") {
        event.preventDefault();
      }
    }

    window.addEventListener("keydown", preventDismiss);
    return () => window.removeEventListener("keydown", preventDismiss);
  }, []);

  return (
    <div aria-busy="true" aria-modal="true" className="release-modal sending-modal" role="dialog">
      <section
        aria-describedby="sending-dialog-description"
        aria-labelledby="sending-dialog-title"
        className="panel release-dialog sending-dialog"
        ref={dialogRef}
        tabIndex="-1"
      >
        <span aria-hidden="true" className="sending-dialog-icon">
          <LoaderCircle className="sending-spinner" size={32} />
        </span>
        <p className="release-dialog-eyebrow">放流準備中</p>
        <h2 id="sending-dialog-title">海へ送っています…</h2>
        <p id="sending-dialog-description">魚を海へ届けています。画面を閉じずに少し待ってね。</p>
      </section>
    </div>
  );
}
