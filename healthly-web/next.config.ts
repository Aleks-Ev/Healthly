import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  // Самодостаточная сборка в .next/standalone (сервер + нужные node_modules) — для компактного Docker-образа
  output: "standalone",
  experimental: {
    agentFeedback: true,
  },
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
