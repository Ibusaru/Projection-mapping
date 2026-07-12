import React, { useState } from "react";
import { KeyRound, Waves } from "lucide-react";

export function AdminLogin({ error, onSubmit, submitting }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  function handleSubmit(event) {
    event.preventDefault();
    onSubmit(email.trim(), password);
  }

  return (
    <main className="admin-login-shell">
      <section className="admin-login-card">
        <div className="admin-brand-mark" aria-hidden="true">
          <Waves size={28} />
        </div>
        <p className="admin-eyebrow">OCEAN CONTROL</p>
        <h1>管理者ログイン</h1>
        <p className="admin-login-copy">水槽の魚とカメラを、安全に操作します。</p>

        <form className="admin-login-form" onSubmit={handleSubmit}>
          <label>
            メールアドレス
            <input
              autoComplete="username"
              onChange={(event) => setEmail(event.target.value)}
              required
              type="email"
              value={email}
            />
          </label>
          <label>
            パスワード
            <input
              autoComplete="current-password"
              minLength={6}
              onChange={(event) => setPassword(event.target.value)}
              required
              type="password"
              value={password}
            />
          </label>
          {error ? <p className="admin-form-error" role="alert">{error}</p> : null}
          <button className="admin-primary-button" disabled={submitting} type="submit">
            <KeyRound size={18} />
            {submitting ? "確認中…" : "ログイン"}
          </button>
        </form>
        <a className="admin-back-link" href="/">魚をつくる画面へ</a>
      </section>
    </main>
  );
}
