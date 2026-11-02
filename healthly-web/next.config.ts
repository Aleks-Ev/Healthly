import type { NextConfig } from "next";

// Один адрес для сайта и API (публичный демо-сервер через Cloudflare Tunnel, см. TEAM_GUIDE.md):
// API_PROXY_TARGET=http://api:8080 → сервер Next.js проксирует /api/* в API, браузер ходит на тот же домен
// (NEXT_PUBLIC_API_URL=/api). Нет CORS, а cookie с refresh-токеном (SameSite=Strict, Path=/api/auth) остаётся «своей».
// Значение читается при сборке (next build), в Docker — build-аргумент. Не задано — прокси нет (локальная разработка)
const apiProxyTarget = process.env.API_PROXY_TARGET?.replace(/\/+$/, "");

const nextConfig: NextConfig = {
  /* config options here */
  // Самодостаточная сборка в .next/standalone (сервер + нужные node_modules) — для компактного Docker-образа
  output: "standalone",
  experimental: {
    agentFeedback: true,
    // По умолчанию прокси обрывает запрос через 30 с, а ИИ-анализ с переключением Gemini → Groq идёт дольше
    proxyTimeout: 120_000,
  },
  async rewrites() {
    return apiProxyTarget ? [{ source: "/api/:path*", destination: `${apiProxyTarget}/api/:path*` }] : [];
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
