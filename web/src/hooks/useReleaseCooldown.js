import { useCallback, useEffect, useState } from "react";
import { releaseCooldown } from "../config/releaseOptions";

function clearStoredCooldown() {
  try {
    window.localStorage.removeItem(releaseCooldown.storageKey);
  } catch {
    // The timer still works for this page when storage is unavailable.
  }
}

function readStoredCooldown() {
  if (typeof window === "undefined") return 0;

  try {
    const cooldownUntil = Number(window.localStorage.getItem(releaseCooldown.storageKey));
    if (Number.isFinite(cooldownUntil) && cooldownUntil > Date.now()) {
      return cooldownUntil;
    }
    clearStoredCooldown();
  } catch {
    // Ignore blocked or unavailable storage.
  }

  return 0;
}

export function useReleaseCooldown() {
  const [cooldownUntil, setCooldownUntil] = useState(readStoredCooldown);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!cooldownUntil) return undefined;

    function updateTimer() {
      const nextNow = Date.now();
      setNow(nextNow);

      if (nextNow >= cooldownUntil) {
        clearStoredCooldown();
        setCooldownUntil(0);
      }
    }

    updateTimer();
    const intervalId = window.setInterval(updateTimer, 250);
    return () => window.clearInterval(intervalId);
  }, [cooldownUntil]);

  useEffect(() => {
    function syncCooldown(event) {
      if (event.key !== releaseCooldown.storageKey) return;
      setNow(Date.now());
      setCooldownUntil(readStoredCooldown());
    }

    window.addEventListener("storage", syncCooldown);
    return () => window.removeEventListener("storage", syncCooldown);
  }, []);

  const startCooldown = useCallback(() => {
    const nextCooldownUntil = Date.now() + releaseCooldown.durationMs;

    try {
      window.localStorage.setItem(releaseCooldown.storageKey, String(nextCooldownUntil));
    } catch {
      // Continue with the in-memory timer when storage is unavailable.
    }

    setNow(Date.now());
    setCooldownUntil(nextCooldownUntil);
  }, []);

  return {
    remainingSeconds: Math.max(0, Math.ceil((cooldownUntil - now) / 1000)),
    startCooldown,
  };
}
