import { blockedWords } from "../config/fishOptions";

export function normalizeNickname(value) {
  return value.replace(/\s+/g, "").trim();
}

export function validateNickname(value) {
  const nickname = normalizeNickname(value);
  if (nickname.length < 1) return "ニックネームを入力してね";
  if (nickname.length > 12) return "12文字以内にしてね";
  if (/[\r\n]/.test(value)) return "改行は使えません";
  if (/[<>]/.test(value)) return "使えない記号があります";
  if (/^[\p{P}\p{S}]+$/u.test(nickname)) return "文字を1つ以上入れてね";

  const lower = nickname.toLowerCase();
  if (blockedWords.some((word) => lower.includes(word.toLowerCase()))) {
    return "別のニックネームにしてね";
  }

  return "";
}
