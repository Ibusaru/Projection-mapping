function createSubmissionId() {
  if (globalThis.crypto.randomUUID) return globalThis.crypto.randomUUID();
  // randomUUID is unavailable on HTTP LAN previews; getRandomValues still works.
  const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

// Keep the identity of an unsuccessful submission so a lost response cannot
// turn a retry into a second fish. Successful, intentional releases get new IDs.
export function createFishSubmissionWriter(client, bucket, defaults) {
  let pending = null;
  let inFlight = null;

  async function submit({ nickname, blob, size = defaults.size }) {
    const bytes = new Uint8Array(await blob.arrayBuffer());
    const matchesPending = pending
      && pending.payload.nickname === nickname
      && pending.payload.size === size
      && pending.bytes.length === bytes.length
      && pending.bytes.every((value, index) => value === bytes[index]);

    if (!matchesPending) {
      const id = createSubmissionId();
      const path = `${id}.png`;
      const { data } = client.storage.from(bucket).getPublicUrl(path);
      pending = {
        bytes,
        uploaded: false,
        payload: {
          ...defaults,
          id,
          nickname,
          size,
          texture_path: path,
          texture_url: data.publicUrl,
          updated_at: new Date().toISOString(),
        },
      };
    }

    if (!pending.uploaded) {
      const { error } = await client.storage.from(bucket).upload(
        pending.payload.texture_path,
        blob,
        { cacheControl: "60", contentType: "image/png", upsert: false },
      );
      // A previous upload may have succeeded even though its response was lost.
      // This UUID path is reserved for this exact PNG; never overwrite it.
      if (error && String(error.statusCode) !== "409") {
        throw new Error(`画像アップロード: ${error.message}`);
      }
      pending.uploaded = true;
    }

    const { error } = await client.from("fishes").upsert(pending.payload, {
      onConflict: "id",
      ignoreDuplicates: true,
    });
    if (error) throw new Error(`DB登録: ${error.message}`);
    pending = null;
  }

  return function upload(submission) {
    if (inFlight) return inFlight;
    inFlight = submit(submission).finally(() => { inFlight = null; });
    return inFlight;
  };
}
