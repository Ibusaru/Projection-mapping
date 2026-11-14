import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

// The public player reads the same build-time configuration as the web app.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "VITE_");
  const config = { supabaseUrl: env.VITE_SUPABASE_URL || "", supabaseAnonKey: env.VITE_SUPABASE_ANON_KEY || "" };
  if (config.supabaseAnonKey) {
    let role;
    try { role = JSON.parse(Buffer.from(config.supabaseAnonKey.split(".")[1], "base64url").toString()).role; } catch { /* rejected below */ }
    if (role !== "anon") throw new Error("VITE_SUPABASE_ANON_KEY must be a legacy anon JWT, never a secret or service role key.");
  }
  const body = JSON.stringify(config);
  return {
    plugins: [react(), {
      name: "ocean-public-config",
      configureServer(server) {
        server.middlewares.use("/ocean-config.json", (_req, res) => {
          res.setHeader("Content-Type", "application/json");
          res.setHeader("Cache-Control", "no-store");
          res.end(body);
        });
      },
      generateBundle() { this.emitFile({ type: "asset", fileName: "ocean-config.json", source: body }); },
    }],
  };
});
