// Единый клиент для запросов к HealthlyApi.
// Подставляет JWT, разбирает ошибки API в ApiError и даёт типизированные функции для каждого эндпоинта.

import type {
  AuthResponse,
  ChangePasswordRequest,
  CreateUserRequest,
  DocumentStatus,
  IndicatorTrend,
  LoginRequest,
  MedicalDocument,
  MedicalDocumentDetails,
  RegisterRequest,
  Role,
  TestResult,
  TestResultRequest,
  UpdateUserRequest,
  User,
  UserReport,
} from '@/types/api';

export const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5097/api';

// --- Токены ---
// Access-токен (JWT, 15 минут) хранится только в памяти вкладки — его не прочитать из localStorage.
// Refresh-токен (30 дней) хранится в httpOnly-cookie, которую ставит API: JavaScript её вообще не видит,
// браузер сам отправляет её на /api/auth/* (поэтому все запросы идут с credentials: 'include').
// После перезагрузки страницы access-токена нет — AuthProvider получает новый через restoreSession().

let accessToken: string | null = null;

// Флаг «пользователь входил» — не секрет; нужен, чтобы гостю не слать лишний /auth/refresh при каждой загрузке
const SESSION_FLAG_KEY = 'healthly_has_session';
// Ключи старой версии, где токены лежали в localStorage, — удаляем при первом запуске
const LEGACY_KEYS = ['healthly_token', 'healthly_refresh_token'];

function writeStorage(key: string, value: string | null): void {
  try {
    if (value) {
      window.localStorage.setItem(key, value);
    } else {
      window.localStorage.removeItem(key);
    }
  } catch {
    // localStorage недоступен (приватный режим) — флаг не сохранится, сессия всё равно восстановится по cookie
  }
}

export function hasStoredSession(): boolean {
  try {
    LEGACY_KEYS.forEach((key) => window.localStorage.removeItem(key));
    return window.localStorage.getItem(SESSION_FLAG_KEY) === '1';
  } catch {
    return true; // флаг недоступен — пробуем восстановить сессию по cookie
  }
}

// Сохранить access-токен из ответа входа/регистрации/обновления; null — выйти
export function setSession(auth: Pick<AuthResponse, 'token'> | null): void {
  accessToken = auth?.token ?? null;
  writeStorage(SESSION_FLAG_KEY, auth ? '1' : null);
}

// AuthProvider подписывается сюда, чтобы разлогинить пользователя, когда сессию продлить не удалось
let onUnauthorized: (() => void) | null = null;

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler;
}

// --- Ошибки ---

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    // Ошибки валидации по полям: { Email: ["The Email field is not a valid e-mail address."] }
    public fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

// Все ошибки API приходят в формате ProblemDetails (RFC 9457)
interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: ProblemDetails = {};
  try {
    body = (await response.json()) as ProblemDetails;
  } catch {
    // тело пустое или не JSON (например, ответ прокси)
  }

  const fieldErrors = body.errors ?? {};
  const firstFieldError = Object.values(fieldErrors)[0]?.[0];
  // У ошибок валидации detail нет — показываем первую ошибку поля
  const message = body.detail ?? firstFieldError ?? body.title ?? `Request failed (${response.status})`;
  return new ApiError(response.status, message, fieldErrors);
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message;
  }
  if (error instanceof TypeError) {
    return 'Cannot reach the server. Is the API running?';
  }
  return error instanceof Error ? error.message : 'An unknown error occurred';
}

// --- Продление сессии (refresh-токен в httpOnly-cookie) ---

// API просит заголовком выдать refresh-токен в cookie, а не в теле ответа (см. RefreshTokenCookie на бэкенде)
const COOKIE_TRANSPORT = { 'X-Refresh-Token-Transport': 'cookie' };

async function postRefresh(): Promise<AuthResponse | null> {
  const response = await fetch(`${API_URL}/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: '{}', // токен браузер отправит сам — в cookie
  });
  return response.ok ? ((await response.json()) as AuthResponse) : null;
}

// Один общий запрос обновления: если несколько запросов одновременно получили 401, все ждут его,
// а не отправляют refresh-токен по нескольку раз (повторное использование API считает кражей)
let refreshInFlight: Promise<AuthResponse | null> | null = null;

export function refreshSession(): Promise<AuthResponse | null> {
  refreshInFlight ??= (async () => {
    try {
      let auth = await postRefresh();
      if (!auth) {
        // Другая вкладка могла только что обменять ту же cookie: браузер уже получил новую — пробуем ещё раз
        await new Promise((resolve) => setTimeout(resolve, 500));
        auth = await postRefresh();
      }
      setSession(auth);
      return auth;
    } catch {
      return null; // сеть недоступна — сессию не трогаем
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

// --- Базовый запрос ---

// Здесь 401 означает неверный пароль или недействительный refresh-токен — продлевать сессию бессмысленно
const NO_REFRESH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  json?: unknown;
  formData?: FormData;
  headers?: Record<string, string>;
}

function fetchApi(path: string, { method = 'GET', json, formData, headers: extraHeaders }: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = { ...extraHeaders };

  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }
  if (json !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  return fetch(`${API_URL}${path}`, {
    method,
    headers,
    // Cookie с refresh-токеном ограничена путём /api/auth, на другие адреса браузер её не отправит
    credentials: 'include',
    body: formData ?? (json !== undefined ? JSON.stringify(json) : undefined),
  });
}

async function send(path: string, options: RequestOptions = {}): Promise<Response> {
  let response = await fetchApi(path, options);

  // Access-токен истёк → продлеваем сессию по cookie и один раз повторяем запрос
  if (response.status === 401 && !NO_REFRESH_PATHS.includes(path)) {
    if (await refreshSession()) {
      response = await fetchApi(path, options);
    }
    if (response.status === 401) {
      setSession(null);
      onUnauthorized?.();
    }
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  return response;
}

async function request<T>(path: string, options?: RequestOptions): Promise<T> {
  const response = await send(path, options);
  return (response.status === 204 ? undefined : await response.json()) as T;
}

// --- Эндпоинты ---

export const authApi = {
  register: (data: RegisterRequest) =>
    request<AuthResponse>('/auth/register', { method: 'POST', json: data, headers: COOKIE_TRANSPORT }),
  login: (data: LoginRequest) => request<AuthResponse>('/auth/login', { method: 'POST', json: data, headers: COOKIE_TRANSPORT }),
  me: () => request<User>('/auth/me'),
  // Новая пара токенов: другие сессии пользователя завершаются, текущая продолжается
  changePassword: (data: ChangePasswordRequest) =>
    request<AuthResponse>('/auth/me/password', { method: 'PUT', json: data, headers: COOKIE_TRANSPORT }),
  // Отзывает refresh-токен из cookie и удаляет cookie (access-токен истечёт сам)
  logout: () => request<void>('/auth/logout', { method: 'POST', json: {} }),
};

export const usersApi = {
  list: (role?: Role) => request<User[]>(role ? `/users?role=${role}` : '/users'),
  get: (id: number) => request<User>(`/users/${id}`),
  create: (data: CreateUserRequest) => request<User>('/users', { method: 'POST', json: data }),
  update: (id: number, data: UpdateUserRequest) => request<User>(`/users/${id}`, { method: 'PUT', json: data }),
  remove: (id: number) => request<void>(`/users/${id}`, { method: 'DELETE' }),
  report: (id: number) => request<UserReport>(`/users/${id}/report`),
  trends: (id: number) => request<IndicatorTrend[]>(`/users/${id}/trends`),
};

export const documentsApi = {
  listForUser: (userId: number) => request<MedicalDocument[]>(`/users/${userId}/documents`),
  upload: (userId: number, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return request<MedicalDocumentDetails>(`/users/${userId}/documents`, { method: 'POST', formData });
  },
  get: (id: number) => request<MedicalDocumentDetails>(`/documents/${id}`),
  updateStatus: (id: number, status: DocumentStatus) =>
    request<MedicalDocumentDetails>(`/documents/${id}/status`, { method: 'PUT', json: { status } }),
  remove: (id: number) => request<void>(`/documents/${id}`, { method: 'DELETE' }),
  // Файл нужно скачивать с заголовком Authorization, поэтому обычная ссылка не подходит
  download: async (id: number, fileName: string) => {
    const response = await send(`/documents/${id}/file`);
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  },
};

export const resultsApi = {
  add: (documentId: number, data: TestResultRequest) =>
    request<TestResult>(`/documents/${documentId}/results`, { method: 'POST', json: data }),
  update: (id: number, data: TestResultRequest) => request<TestResult>(`/results/${id}`, { method: 'PUT', json: data }),
  remove: (id: number) => request<void>(`/results/${id}`, { method: 'DELETE' }),
};
