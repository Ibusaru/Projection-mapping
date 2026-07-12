import { createClient } from "@supabase/supabase-js";

const storageBucket = "fish-drawings";
const supabaseUrl = import.meta.env.VITE_SUPABASE_URL;
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY;

export const isAdminBackendConfigured = Boolean(supabaseUrl && supabaseAnonKey);

export const adminSupabase = isAdminBackendConfigured
  ? createClient(supabaseUrl, supabaseAnonKey)
  : null;

function readLocalFishes() {
  try {
    const fishes = JSON.parse(localStorage.getItem("local_fishes") ?? "[]");
    return Array.isArray(fishes) ? fishes : [];
  } catch {
    return [];
  }
}

export async function getAdminSession() {
  if (!adminSupabase) return { user: { email: "local-preview" } };

  const { data, error } = await adminSupabase.auth.getSession();
  if (error) throw error;
  return data.session;
}

export function observeAdminSession(callback) {
  if (!adminSupabase) return () => {};

  const { data } = adminSupabase.auth.onAuthStateChange((_event, session) => callback(session));
  return () => data.subscription.unsubscribe();
}

export async function signInAdmin(email, password) {
  if (!adminSupabase) return { user: { email: "local-preview" } };

  const { data, error } = await adminSupabase.auth.signInWithPassword({ email, password });
  if (error) throw error;
  return data.session;
}

export async function signOutAdmin() {
  if (!adminSupabase) return;
  const { error } = await adminSupabase.auth.signOut();
  if (error) throw error;
}

export async function verifyAdminAccess() {
  if (!adminSupabase) return true;

  const { data, error } = await adminSupabase.rpc("is_admin");
  if (error) throw error;
  return data === true;
}

export async function fetchAdminFishes() {
  if (!adminSupabase) {
    return readLocalFishes().sort((a, b) => String(b.created_at).localeCompare(String(a.created_at)));
  }

  const { data, error } = await adminSupabase
    .from("fishes")
    .select("id,nickname,species,texture_path,texture_url,created_at")
    .order("created_at", { ascending: false });

  if (error) throw error;
  return data ?? [];
}

export async function issueCameraCommand(action, fish = null) {
  if (!adminSupabase) return;

  const payload = fish
    ? { fish_id: fish.id, nickname: fish.nickname }
    : {};
  const { error } = await adminSupabase.from("admin_commands").insert({ action, payload });
  if (error) throw error;
}

export async function deleteAdminFish(fish) {
  if (!adminSupabase) {
    const nextFishes = readLocalFishes().filter((item) => item.id !== fish.id);
    localStorage.setItem("local_fishes", JSON.stringify(nextFishes));
    return;
  }

  const { data: texturePath, error } = await adminSupabase.rpc("admin_delete_fish", {
    target_id: fish.id,
  });
  if (error) throw error;

  if (texturePath) {
    const { error: storageError } = await adminSupabase.storage
      .from(storageBucket)
      .remove([texturePath]);
    if (storageError) {
      console.warn("魚データは削除しましたが、画像の削除に失敗しました", storageError);
    }
  }
}
