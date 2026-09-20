import { createClient } from "@supabase/supabase-js";
import { defaultFishPayload } from "../config/fishOptions";
import { createFishSubmissionWriter } from "./fishSubmission";

const storageBucket = "fish-drawings";
const supabaseUrl = import.meta.env.VITE_SUPABASE_URL;
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY;
// Public submissions must stay anonymous even if /admin has a persisted login.
const supabase =
  supabaseUrl && supabaseAnonKey
    ? createClient(supabaseUrl, supabaseAnonKey, {
        auth: {
          autoRefreshToken: false,
          detectSessionInUrl: false,
          persistSession: false,
        },
      })
    : null;

const submitRemoteFish = supabase
  ? createFishSubmissionWriter(supabase, storageBucket, defaultFishPayload)
  : null;

function createId() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }

  return `local-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function readLocalFishes() {
  try {
    const parsed = JSON.parse(localStorage.getItem("local_fishes") ?? "[]");
    return Array.isArray(parsed) ? parsed : [];
  } catch (error) {
    console.warn("ローカルの魚データを読み込めませんでした", error);
    return [];
  }
}

function sanitizePathPart(value) {
  return encodeURIComponent(value)
    .replace(/%/g, "")
    .replace(/[^a-zA-Z0-9_-]/g, "")
    .slice(0, 48);
}

export async function uploadFishDrawing({ nickname, blob, size = defaultFishPayload.size }) {
  if (!supabase) {
    const localFish = readLocalFishes();
    const id = createId();
    const safeName = sanitizePathPart(nickname) || "fish";
    const publicUrl = URL.createObjectURL(blob);
    const nextFish = {
      ...defaultFishPayload,
      id,
      nickname,
      size,
      texture_path: `local/${safeName}/${id}.png`,
      texture_url: publicUrl,
      created_at: new Date().toISOString(),
      updated_at: new Date().toISOString(),
    };
    localStorage.setItem("local_fishes", JSON.stringify([...localFish, nextFish]));
    return;
  }

  return submitRemoteFish({ nickname, blob, size });
}
