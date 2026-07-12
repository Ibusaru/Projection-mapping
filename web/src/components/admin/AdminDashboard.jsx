import React, { useEffect, useMemo, useState } from "react";
import {
  ArrowUpFromLine,
  Camera,
  Fish,
  Focus,
  LogOut,
  RefreshCw,
  Search,
  Trash2,
  Waves,
} from "lucide-react";
import {
  deleteAdminFish,
  fetchAdminFishes,
  isAdminBackendConfigured,
  issueCameraCommand,
  signOutAdmin,
} from "../../data/adminStore";

const speciesLabels = {
  clownfish: "クマノミ",
  jellyfish: "クラゲ",
  tuna: "マグロ",
  original: "オリジナル",
};

function formatDate(value) {
  if (!value) return "日時不明";
  return new Intl.DateTimeFormat("ja-JP", {
    month: "numeric",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
}

export function AdminDashboard({ session, onSignedOut }) {
  const [fishes, setFishes] = useState([]);
  const [selectedId, setSelectedId] = useState("");
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [busyAction, setBusyAction] = useState("");
  const [pendingDeleteId, setPendingDeleteId] = useState("");
  const [notice, setNotice] = useState(null);

  const selectedFish = useMemo(
    () => fishes.find((fish) => fish.id === selectedId) ?? null,
    [fishes, selectedId],
  );
  const shownFishes = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase("ja");
    if (!needle) return fishes;
    return fishes.filter((fish) => fish.nickname.toLocaleLowerCase("ja").includes(needle));
  }, [fishes, query]);

  async function refreshFishes(showNotice = false) {
    setLoading(true);
    try {
      const nextFishes = await fetchAdminFishes();
      setFishes(nextFishes);
      setSelectedId((current) => nextFishes.some((fish) => fish.id === current) ? current : "");
      if (showNotice) setNotice({ type: "success", text: "魚一覧を更新しました" });
    } catch (error) {
      setNotice({ type: "error", text: `魚一覧を取得できません: ${error.message}` });
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    refreshFishes();
  }, []);

  async function runCameraCommand(action, successText, fish = null) {
    setBusyAction(action);
    setNotice(null);
    try {
      await issueCameraCommand(action, fish);
      setNotice({ type: "success", text: successText });
    } catch (error) {
      setNotice({ type: "error", text: `カメラ命令を送れません: ${error.message}` });
    } finally {
      setBusyAction("");
    }
  }

  async function handleDelete(fish) {
    if (pendingDeleteId !== fish.id) {
      setPendingDeleteId(fish.id);
      return;
    }

    setBusyAction(`delete-${fish.id}`);
    setNotice(null);
    try {
      await deleteAdminFish(fish);
      setFishes((current) => current.filter((item) => item.id !== fish.id));
      setSelectedId((current) => current === fish.id ? "" : current);
      setPendingDeleteId("");
      setNotice({ type: "success", text: `「${fish.nickname}」を削除しました` });
    } catch (error) {
      setNotice({ type: "error", text: `削除できません: ${error.message}` });
    } finally {
      setBusyAction("");
    }
  }

  async function handleSignOut() {
    await signOutAdmin();
    onSignedOut();
  }

  return (
    <main className="admin-shell">
      <header className="admin-topbar">
        <div className="admin-title-group">
          <span className="admin-logo"><Waves size={24} /></span>
          <div>
            <p className="admin-eyebrow">OCEAN CONTROL</p>
            <h1>水槽管理</h1>
          </div>
        </div>
        <div className="admin-account">
          <span>{session?.user?.email}</span>
          <button aria-label="ログアウト" className="admin-icon-button" onClick={handleSignOut} type="button">
            <LogOut size={18} />
          </button>
        </div>
      </header>

      {!isAdminBackendConfigured ? (
        <p className="admin-demo-banner">ローカルプレビューです。カメラ命令は送信されません。</p>
      ) : null}

      {notice ? <p className={`admin-notice ${notice.type}`} role="status">{notice.text}</p> : null}

      <section className="admin-camera-panel">
        <div className="admin-section-heading">
          <div>
            <p className="admin-eyebrow">CAMERA</p>
            <h2>カメラ制御</h2>
          </div>
          <Camera size={22} />
        </div>
        <div className="admin-camera-actions">
          <button
            disabled={Boolean(busyAction)}
            onClick={() => runCameraCommand("camera_aerial", "カメラを上空へ移動します")}
            type="button"
          >
            <ArrowUpFromLine size={21} />
            <span><strong>上空へ</strong><small>水槽全体を見渡す</small></span>
          </button>
          <button
            disabled={Boolean(busyAction)}
            onClick={() => runCameraCommand("camera_roam", "自動回遊に切り替えました")}
            type="button"
          >
            <Waves size={21} />
            <span><strong>回遊</strong><small>自動ツアーに戻る</small></span>
          </button>
          <button
            className="focus-command"
            disabled={!selectedFish || Boolean(busyAction)}
            onClick={() => runCameraCommand(
              "camera_focus",
              `「${selectedFish.nickname}」にフォーカスします`,
              selectedFish,
            )}
            type="button"
          >
            <Focus size={21} />
            <span>
              <strong>魚にフォーカス</strong>
              <small>{selectedFish ? selectedFish.nickname : "下の一覧から魚を選択"}</small>
            </span>
          </button>
        </div>
      </section>

      <section className="admin-fish-panel">
        <div className="admin-section-heading fish-heading">
          <div>
            <p className="admin-eyebrow">FISH</p>
            <h2>魚の管理 <span>{fishes.length}</span></h2>
          </div>
          <button className="admin-refresh-button" disabled={loading} onClick={() => refreshFishes(true)} type="button">
            <RefreshCw className={loading ? "is-spinning" : ""} size={17} />
            更新
          </button>
        </div>

        <label className="admin-search">
          <Search size={18} />
          <input
            onChange={(event) => setQuery(event.target.value)}
            placeholder="ニックネームで検索"
            type="search"
            value={query}
          />
        </label>

        {loading ? <p className="admin-empty">魚を読み込んでいます…</p> : null}
        {!loading && shownFishes.length === 0 ? (
          <div className="admin-empty"><Fish size={28} /><p>該当する魚はいません</p></div>
        ) : null}

        <div className="admin-fish-list">
          {shownFishes.map((fish) => {
            const selected = fish.id === selectedId;
            const confirming = fish.id === pendingDeleteId;
            return (
              <article className={selected ? "admin-fish-row selected" : "admin-fish-row"} key={fish.id}>
                <button className="admin-fish-select" onClick={() => setSelectedId(fish.id)} type="button">
                  <span className="admin-fish-thumb">
                    {fish.texture_url ? <img alt="" src={fish.texture_url} /> : <Fish size={24} />}
                  </span>
                  <span className="admin-fish-info">
                    <strong>{fish.nickname}</strong>
                    <small>{speciesLabels[fish.species] ?? fish.species} · {formatDate(fish.created_at)}</small>
                  </span>
                  <span className="admin-radio" aria-hidden="true" />
                </button>
                <button
                  aria-label={confirming ? `「${fish.nickname}」を完全に削除` : `「${fish.nickname}」を削除`}
                  className={confirming ? "admin-delete-button confirming" : "admin-delete-button"}
                  disabled={busyAction === `delete-${fish.id}`}
                  onBlur={() => setPendingDeleteId((current) => current === fish.id ? "" : current)}
                  onClick={() => handleDelete(fish)}
                  type="button"
                >
                  <Trash2 size={17} />
                  <span>
                    {busyAction === `delete-${fish.id}` ? "削除中" : confirming ? "もう一度押して削除" : "削除"}
                  </span>
                </button>
              </article>
            );
          })}
        </div>
      </section>
    </main>
  );
}
