import { blockedWordPatterns, blockedWords } from "../config/fishOptions";

export const BLANK_NICKNAME_MESSAGE = "空白だけのニックネームは使えません";
export const NG_NICKNAME_MESSAGE = "NGワードです";

export function normalizeNickname(value) {
  return value.replace(/\s+/g, "").trim();
}

function normalizeForBlockedWordCheck(value) {
  return normalizeNickname(value)
    .normalize("NFKC")
    .toLowerCase()
    .replace(/[\p{P}\p{S}_ー～〜・]/gu, "");
}

function normalizeLeetspeak(value) {
  return value
    .replace(/[4@]/g, "a")
    .replace(/[3]/g, "e")
    .replace(/[1!|]/g, "i")
    .replace(/[0]/g, "o")
    .replace(/[5$]/g, "s")
    .replace(/[7]/g, "t");
}

export function validateNickname(value) {
  const nickname = normalizeNickname(value);
  if (nickname.length < 1) {
    return value.length > 0 ? BLANK_NICKNAME_MESSAGE : "ニックネームを入力してね";
  }
  if (nickname.length > 12) return "12文字以内にしてね";
  if (/[\r\n]/.test(value)) return "改行は使えません";
  if (/[<>]/.test(value)) return "使えない記号があります";
  if (/^[\p{P}\p{S}]+$/u.test(nickname)) return "文字を1つ以上入れてね";

  const patternNickname = normalizeNickname(nickname).normalize("NFKC").toLowerCase();
  const blockedNickname = normalizeLeetspeak(normalizeForBlockedWordCheck(nickname));
  if (
    blockedWords.some((word) => blockedNickname.includes(normalizeLeetspeak(normalizeForBlockedWordCheck(word)))) ||
    blockedWordPatterns.some((pattern) => pattern.test(patternNickname))
  ) {
    return NG_NICKNAME_MESSAGE;
  }

  return "";
}
