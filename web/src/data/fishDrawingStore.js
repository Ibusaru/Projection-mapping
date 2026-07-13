import { createClient } from "@supabase/supabase-js";
import { defaultFishPayload } from "../config/fishOptions";

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

  const safeName = sanitizePathPart(nickname) || createId();
  const texturePath = `${safeName}/${Date.now()}-${createId()}.png`;

  const { error: uploadError } = await supabase.storage
    .from(storageBucket)
    .upload(texturePath, blob, {
      cacheControl: "60",
      contentType: "image/png",
      upsert: false,
    });

  if (uploadError) {
    throw new Error(`画像アップロード: ${uploadError.message}`);
  }

  const { data: publicData } = supabase.storage.from(storageBucket).getPublicUrl(texturePath);
  const textureUrl = publicData.publicUrl;

  const payload = {
    ...defaultFishPayload,
    nickname,
    size,
    texture_path: texturePath,
    texture_url: textureUrl,
    updated_at: new Date().toISOString(),
  };

  const { error: insertError } = await supabase
    .from("fishes")
    .insert(payload);

  if (insertError) {
    throw new Error(`DB登録: ${insertError.message}`);
  }
}
