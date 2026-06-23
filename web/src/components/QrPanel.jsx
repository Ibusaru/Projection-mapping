import React from "react";
import { useEffect, useState } from "react";
import QRCode from "qrcode";

function resolveQrTarget() {
  const configuredUrl = import.meta.env.VITE_PUBLIC_APP_URL?.trim();
  if (configuredUrl) return configuredUrl;
  if (typeof window !== "undefined") return window.location.href;
  return "";
}

export function QrPanel() {
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
    <section className="field-block qr-panel">
      <div className="qr-copy">
        <h2>掲示用QRコード</h2>
        <p>デプロイ後に `VITE_PUBLIC_APP_URL` を設定すると、このQRが本番URLを指します。</p>
      </div>
      <div className="qr-card">
        {qrCodeDataUrl ? (
          <img alt="このアプリを開くQRコード" className="qr-image" src={qrCodeDataUrl} />
        ) : (
          <div className="qr-placeholder">QRを生成できませんでした</div>
        )}
        <a className="qr-link" href={qrTarget} rel="noreferrer" target="_blank">
          {qrTarget || "URL未設定"}
        </a>
      </div>
    </section>
  );
}
