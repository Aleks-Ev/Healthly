# Образ Next.js-фронтенда (сборка: docker compose build web)
# Трёхэтапная сборка: зависимости → next build → минимальный образ с standalone-сервером.

# --- 1. Зависимости (кешируются, пока не меняется package-lock.json) ---
FROM node:24-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# --- 2. Сборка ---
FROM node:24-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Адрес API «вшивается» в клиентский JS при сборке (запросы идут из браузера пользователя),
# поэтому это build-аргумент, а не переменная окружения контейнера
ARG NEXT_PUBLIC_API_URL=http://localhost:5097/api
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL \
    NEXT_TELEMETRY_DISABLED=1
RUN npm run build

# --- 3. Рантайм ---
FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# standalone содержит server.js и только нужные зависимости; статику и public копируем отдельно
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static
COPY --from=build --chown=node:node /app/public ./public

USER node
EXPOSE 3000
CMD ["node", "server.js"]
