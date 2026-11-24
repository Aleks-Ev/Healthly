import type { NextConfig } from "next";

// Один адрес для сайта и API (публичный демо-сервер через Cloudflare Tunnel, см. TEAM_GUIDE.md):
// API_PROXY_TARGET=http://api:8080 → сервер Next.js проксирует /api/* в API, браузер ходит на тот же домен
// (NEXT_PUBLIC_API_URL=/api). Нет CORS, а cookie с refresh-токеном (SameSite=Strict, Path=/api/auth) остаётся «своей».
// Значение читается при сборке (next build), в Docker — build-аргумент. Не задано — прокси нет (локальная разработка)
const apiProxyTarget = process.env.API_PROXY_TARGET?.replace(/\/+$/, "");

const isDev = process.env.NODE_ENV !== "production";

// Откуда браузер ходит в API (как в lib/api.ts): "/api" — тот же домен ('self'), иначе — отдельный адрес
function apiOrigin(): string {
  try {
    return new URL(process.env.NEXT_PUBLIC_API_URL || "http://localhost:5097/api").origin;
  } catch {
    return ""; // относительный адрес "/api" — покрыт 'self'
  }
}

// Content-Security-Policy: браузер выполняет и загружает только то, что разрешено здесь. Если злоумышленник
// всё же вставит на страницу HTML (XSS), чужой скрипт с другого домена не загрузится, а данные не уйдут на чужой сервер
// (connect-src — только наш сайт и API).
// 'unsafe-inline' для скриптов нужен Next.js (встроенные скрипты гидрации); строгая CSP с nonce потребовала бы
// динамического рендеринга каждой страницы и несовместима с cacheComponents/статикой. В разработке ещё нужны
// 'unsafe-eval' и ws: (горячая перезагрузка). upgrade-insecure-requests не ставим: локальный Docker работает по http.
const contentSecurityPolicy = [
  "default-src 'self'",
  `script-src 'self' 'unsafe-inline'${isDev ? " 'unsafe-eval'" : ""}`,
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  `connect-src 'self' ${apiOrigin()}${isDev ? " ws: wss:" : ""}`.trim(),
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'none'",
].join("; ");

const securityHeaders = [
  { key: "Content-Security-Policy", value: contentSecurityPolicy },
  // Только HTTPS на год (браузер игнорирует заголовок, пришедший по http, — локально ничего не ломает)
  { key: "Strict-Transport-Security", value: "max-age=31536000" },
  // Сайт нельзя встроить в чужую страницу (clickjacking: невидимый фрейм поверх «безобидной» кнопки)
  { key: "X-Frame-Options", value: "DENY" },
  // Браузер не «угадывает» тип файла по содержимому
  { key: "X-Content-Type-Options", value: "nosniff" },
  // Чужим сайтам уходит только домен, без пути (/documents/42)
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  // Камера, микрофон, геолокация сайту не нужны — запрещаем и встроенным скриптам
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=(), usb=()" },
  { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
];

const nextConfig: NextConfig = {
  // Не сообщать «X-Powered-By: Next.js» — меньше подсказок атакующему о версии и стеке
  poweredByHeader: false,
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
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
