import React, { useEffect, useState } from "react";
import { AdminDashboard } from "./components/admin/AdminDashboard";
import { AdminLogin } from "./components/admin/AdminLogin";
import {
  getAdminSession,
  isAdminBackendConfigured,
  observeAdminSession,
  signInAdmin,
  signOutAdmin,
  verifyAdminAccess,
} from "./data/adminStore";

export function AdminApp() {
  const [session, setSession] = useState(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  async function acceptSession(nextSession) {
    if (!nextSession) {
      setSession(null);
      return;
    }

    try {
      const allowed = await verifyAdminAccess();
      if (!allowed) {
        await signOutAdmin();
        setError("このアカウントには管理者権限がありません。");
        setSession(null);
        return;
      }
      setSession(nextSession);
      setError("");
    } catch (accessError) {
      setError(`管理者設定を確認できません: ${accessError.message}`);
      setSession(null);
    }
  }

  useEffect(() => {
    let active = true;
    getAdminSession()
      .then((nextSession) => active && acceptSession(nextSession))
      .catch((sessionError) => active && setError(sessionError.message))
      .finally(() => active && setLoading(false));

    const unsubscribe = observeAdminSession((nextSession) => {
      if (active && !nextSession) setSession(null);
    });
    return () => {
      active = false;
      unsubscribe();
    };
  }, []);

  async function handleLogin(email, password) {
    setSubmitting(true);
    setError("");
    try {
      const nextSession = await signInAdmin(email, password);
      await acceptSession(nextSession);
    } catch (loginError) {
      setError(loginError.message === "Invalid login credentials"
        ? "メールアドレスまたはパスワードが違います。"
        : loginError.message);
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) return <main className="admin-loading">管理画面を準備しています…</main>;
  if (!session && isAdminBackendConfigured) {
    return <AdminLogin error={error} onSubmit={handleLogin} submitting={submitting} />;
  }

  return <AdminDashboard onSignedOut={() => setSession(null)} session={session} />;
}
