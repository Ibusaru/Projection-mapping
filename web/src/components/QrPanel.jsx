import React, { useEffect, useState } from "react";
import { ExternalLink, X } from "lucide-react";
import QRCode from "qrcode";

function resolveQrTarget() {
  const configuredUrl = import.meta.env.VITE_PUBLIC_APP_URL?.trim();
  if (configuredUrl) return configuredUrl;
  if (typeof window !== "undefined") return window.location.href;
  return "";
}

export function QrPanel({ onClose }) {
  const [qrCodeDataUrl, setQrCodeDataUrl] = useState("");
  const qrTarget = resolveQrTarget();

  useEffect(() => {
    let active = true;

    async function generateQrCode() {
      if (!qrTarget) {
        setQrCodeDataUrl("");
        return;
      }

      try {
        const dataUrl = await QRCode.toDataURL(qrTarget, {
          errorCorrectionLevel: "M",
          margin: 1,
          scale: 8,
          color: {
            dark: "#06222f",
            light: "#f7fffe",
          },
        });

        if (active) {
          setQrCodeDataUrl(dataUrl);
        }
      } catch (error) {
        console.error(error);
        if (active) {
          setQrCodeDataUrl("");
        }
      }
    }

    generateQrCode();

    return () => {
      active = false;
    };
  }, [qrTarget]);

  return (
    <div aria-modal="true" className="qr-modal" onClick={onClose} role="dialog">
      <section className="field-block qr-panel" onClick={(event) => event.stopPropagation()}>
        <div className="qr-modal-header">
          <h2>QRコード</h2>
          <button aria-label="閉じる" className="qr-close" onClick={onClose} type="button">
            <X size={18} />
          </button>
        </div>
        <div className="qr-card">
          {qrCodeDataUrl ? (
            <img alt="このアプリを開くQRコード" className="qr-image" src={qrCodeDataUrl} />
          ) : (
            <div className="qr-placeholder">QRコードを生成できませんでした</div>
          )}
          <a className="qr-link" href={qrTarget} rel="noreferrer" target="_blank">
            <ExternalLink size={14} />
            {qrTarget || "URL未設定"}
          </a>
        </div>
      </section>
    </div>
  );
}
