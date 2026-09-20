import assert from "node:assert/strict";
import test from "node:test";
import { createClient } from "@supabase/supabase-js";
import { createFishSubmissionWriter } from "../src/data/fishSubmission.js";

// Exercise the actual SDK requests without writing fish to the production DB.
function fixture({ loseInsertResponse = false, loseUploadResponse = false } = {}) {
  const rows = new Map();
  const images = new Set();
  let uploads = 0;
  let inserts = 0;
  const client = createClient("https://test.supabase.co", "test-key", {
    auth: { persistSession: false, autoRefreshToken: false },
    global: {
      fetch: async (input, options) => {
        const url = new URL(input);
        if (url.pathname.startsWith("/storage/v1/object/")) {
          uploads++;
          if (images.has(url.pathname)) {
            return Response.json({ statusCode: "409", error: "Duplicate", message: "exists" }, { status: 400 });
          }
          images.add(url.pathname);
          if (loseUploadResponse) {
            loseUploadResponse = false;
            throw new TypeError("Upload response lost");
          }
          return Response.json({ Key: url.pathname });
        }
        assert.equal(url.pathname, "/rest/v1/fishes");
        assert.equal(options.method, "POST");
        assert.equal(url.searchParams.get("on_conflict"), "id");
        assert.match(new Headers(options.headers).get("Prefer"), /resolution=ignore-duplicates/);
        inserts++;
        const payload = JSON.parse(options.body);
        if (!rows.has(payload.id)) rows.set(payload.id, payload);
        if (loseInsertResponse) {
          loseInsertResponse = false;
          throw new TypeError("Insert response lost");
        }
        return new Response(null, { status: 201 });
      },
    },
  });
  return {
    write: createFishSubmissionWriter(client, "fish-drawings", { size: "medium" }),
    rows, images,
    counts: () => ({ uploads, inserts }),
  };
}

const submission = (image = "drawing") => ({ nickname: "さかな", blob: new Blob([image], { type: "image/png" }) });

test("simultaneous releases share one request; a later intentional release is separate", async () => {
  const f = fixture();
  const first = f.write(submission());
  assert.equal(f.write(submission()), first);
  await first;
  assert.equal(f.rows.size, 1);
  assert.deepEqual(f.counts(), { uploads: 1, inserts: 1 });
  await f.write(submission());
  assert.equal(f.rows.size, 2);
});

test("retry after a lost DB response preserves the fish ID and PNG", async () => {
  const f = fixture({ loseInsertResponse: true });
  await assert.rejects(f.write(submission()), /DB登録/);
  const original = [...f.rows.values()][0];
  await f.write(submission());
  assert.equal(f.rows.size, 1);
  assert.deepEqual([...f.rows.values()][0], original);
  assert.deepEqual(f.counts(), { uploads: 1, inserts: 2 });
});

test("retry recovers a lost upload response without overwriting the image", async () => {
  const f = fixture({ loseUploadResponse: true });
  await assert.rejects(f.write(submission()), /画像アップロード/);
  assert.equal(f.rows.size, 0);
  await f.write(submission());
  assert.equal(f.images.size, 1);
  assert.equal(f.rows.size, 1);
});

test("a changed drawing after failure gets its own identity", async () => {
  const f = fixture({ loseInsertResponse: true });
  await assert.rejects(f.write(submission()), /DB登録/);
  await f.write(submission("different drawing"));
  assert.equal(f.rows.size, 2);
  assert.equal(new Set([...f.rows.values()].map((row) => row.texture_url)).size, 2);
});
