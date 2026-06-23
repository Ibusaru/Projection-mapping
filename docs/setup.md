# セットアップ手順

## 1. GitHub管理

このリポジトリは以下の構成で管理する。

```text
repository-root/
  spec.md
  web/
  unity/
  docs/
```

Codexに「コミットして」「GitHubにpushして」と依頼すると、この作業フォルダ内の変更を確認してGit操作できる。初回だけGitHub側で空のリポジトリを作り、ローカルにremoteを設定する必要がある。

```powershell
git remote add origin https://github.com/YOUR_NAME/YOUR_REPO.git
git branch -M main
git push -u origin main
```

## 2. Supabase

1. Supabaseで新規プロジェクトを作る。
2. SQL Editorを開く。
3. `docs/supabase.sql` の内容を実行する。
4. Project Settings > API から `Project URL` と `anon public key` を確認する。
5. `web/.env.example` を参考に `web/.env.local` を作る。

```text
VITE_SUPABASE_URL=https://your-project.supabase.co
VITE_SUPABASE_ANON_KEY=your-anon-key
VITE_PUBLIC_APP_URL=https://your-app-url.example.com
```

## 3. Webアプリ

```powershell
cd web
npm install
npm run dev
```

スマホから確認する場合は、同じWi-Fiに接続してPCのローカルIPにアクセスする。

```text
http://PCのIPアドレス:5173
```

本番公開はVercelまたはRenderで行う。環境変数として `VITE_SUPABASE_URL` `VITE_SUPABASE_ANON_KEY` `VITE_PUBLIC_APP_URL` を設定する。`VITE_PUBLIC_APP_URL` を設定すると、アプリ内のQRコードが本番URLを指す。

## 4. Unity

1. Unityプロジェクトを `unity/` に作成する。
2. `unity/Scripts` のC#ファイルをUnityの `Assets/Scripts` に置く。
3. 空のGameObjectを作り、`FishApiClient` と `FishSpawner` をアタッチする。
4. `FishApiClient` にSupabase URLとanon keyを設定する。
5. 魚のPrefabを `FishSpawner` に設定する。
6. Playして、Webから放流した魚がスポーンするか確認する。

## 5. 投影テスト

1. Unityをフルスクリーン表示する。
2. OBSでUnity画面を取り込む。
3. MapMap、HeavyM、TouchDesignerなどにOBS出力または画面キャプチャを渡す。
4. 壁面をメイン、天井を背景演出として補正する。
5. 魚やニックネームが壁と天井の境目に出すぎないようUnity側のカメラを調整する。
