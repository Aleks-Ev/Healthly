'use client';

// Контекст авторизации: текущий пользователь, вход, регистрация, выход.
// Access-токен — в памяти (lib/api.ts), refresh-токен — в httpOnly-cookie. При загрузке страницы сессия
// восстанавливается через POST /api/auth/refresh (cookie отправляет браузер), ответ сразу содержит пользователя.
// Истёкший access-токен lib/api.ts продлевает сам; если продлить не удалось — сбрасывает сессию через setUnauthorizedHandler.

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { authApi, hasStoredSession, refreshSession, setSession, setUnauthorizedHandler } from '@/lib/api';
import type { AuthResponse, ChangePasswordRequest, LoginRequest, RegisterRequest, Role, User } from '@/types/api';

interface AuthContextValue {
  user: User | null;
  // true, пока проверяем сохранённый токен при загрузке страницы
  isLoading: boolean;
  login: (data: LoginRequest) => Promise<User>;
  register: (data: RegisterRequest) => Promise<User>;
  logout: () => void;
  // Смена пароля: остальные сессии завершаются, эта продолжается с новыми токенами
  changePassword: (data: ChangePasswordRequest) => Promise<void>;
  // Обновить данные пользователя после редактирования профиля
  setUser: (user: User) => void;
  hasRole: (...roles: Role[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Сессия на сервере уже недействительна (refresh не удался) — просто забываем пользователя
  const clearSession = useCallback(() => {
    setSession(null);
    setUser(null);
  }, []);

  // Выход по кнопке: сервер отзывает refresh-токен и удаляет cookie; ошибку сети не показываем — локально всё равно выходим
  const logout = useCallback(() => {
    authApi.logout().catch(() => {});
    clearSession();
  }, [clearSession]);

  useEffect(() => {
    setUnauthorizedHandler(clearSession);

    // Восстанавливаем сессию по cookie: refresh сразу возвращает и access-токен, и пользователя
    const restoreSession = async () => {
      try {
        if (hasStoredSession()) {
          const auth = await refreshSession();
          setUser(auth?.user ?? null);
        }
      } finally {
        setIsLoading(false);
      }
    };
    restoreSession();

    return () => setUnauthorizedHandler(null);
  }, [clearSession]);

  const startSession = useCallback((response: AuthResponse) => {
    setSession(response);
    setUser(response.user);
    return response.user;
  }, []);

  const login = useCallback(async (data: LoginRequest) => startSession(await authApi.login(data)), [startSession]);

  const register = useCallback(async (data: RegisterRequest) => startSession(await authApi.register(data)), [startSession]);

  const changePassword = useCallback(async (data: ChangePasswordRequest) => {
    startSession(await authApi.changePassword(data));
  }, [startSession]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      login,
      register,
      logout,
      changePassword,
      setUser,
      hasRole: (...roles) => user !== null && roles.includes(user.role),
    }),
    [user, isLoading, login, register, logout, changePassword],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used inside <AuthProvider>');
  }
  return context;
}
