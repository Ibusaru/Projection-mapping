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
4. 管理画面を使う場合は `docs/supabase-admin-migration.sql` も実行する。
5. Supabase Dashboardの Authentication > Users で管理者ユーザーを作成し、SQL Editorで次を実行する。

```sql
insert into public.admins (user_id) values ('作成したユーザーのUUID');
```

管理画面は公開URLの `/admin`（ローカルでは `http://localhost:5173/admin`）から開く。

6. Project Settings > API から `Project URL` と `anon public key` を確認する。
7. `web/.env.example` を参考に `web/.env.local` を作る。

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

1. Unityプロジェクトは `unity/OceanProjection` を開く。
2. 実行時コードの本体は `unity/OceanProjection/Assets/Scripts` に置く。
3. `FishApiClient` と `FishSpawner` がシーン内GameObjectにアタッチされていることを確認する。
4. 管理用の `OceanAdminCommandClient` と `OceanAdminCameraController` は実行時に自動追加される。
5. Supabase URLとanon keyは、公開リポジトリへ残さないため `web/.env.local` または環境変数で渡す。
6. 魚のPrefabを `FishSpawner` に設定する。
7. Playして、Webから放流した魚がスポーンするか、管理画面の命令が反映されるか確認する。

Unity Editorはローカル開発用に `web/.env.local` の `VITE_SUPABASE_URL` と `VITE_SUPABASE_ANON_KEY` も読む。うまく読めない場合は、Unity起動前にPowerShellで環境変数を設定する。

```powershell
$env:OCEAN_SUPABASE_URL="https://your-project.supabase.co"
$env:OCEAN_SUPABASE_ANON_KEY="your-anon-key"
```

`FishApiClient` のInspector欄に直接入れた値も使えるが、シーン保存時にキーが残るため共有前に空にする。

## 5. 投影テスト

1. Unityをフルスクリーン表示する。
2. OBSでUnity画面を取り込む。
3. MapMap、HeavyM、TouchDesignerなどにOBS出力または画面キャプチャを渡す。
4. 壁面をメイン、天井を背景演出として補正する。
5. 魚やニックネームが壁と天井の境目に出すぎないようUnity側のカメラを調整する。
