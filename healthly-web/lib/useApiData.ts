'use client';

// Загрузка данных из API в компоненте.
// load — функция запроса, обёрнутая в useCallback (при смене её зависимостей данные загружаются заново).
// reload() — перезагрузить после изменения (создание, удаление и т.д.).
// Ответ устаревшего запроса игнорируется (флаг active), поэтому быстрые переключения не перепутают данные.

import { useCallback, useEffect, useState } from 'react';
import { getErrorMessage } from '@/lib/api';

interface ApiDataState<T> {
  data: T | null;
  error: string | null;
}

export function useApiData<T>(load: () => Promise<T>) {
  const [state, setState] = useState<ApiDataState<T>>({ data: null, error: null });
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let active = true;
    load().then(
      (data) => {
        if (active) {
          setState({ data, error: null });
        }
      },
      (error: unknown) => {
        if (active) {
          setState((prev) => ({ data: prev.data, error: getErrorMessage(error) }));
        }
      },
    );
    return () => {
      active = false;
    };
  }, [load, version]);

  const reload = useCallback(() => setVersion((v) => v + 1), []);
  // Подставить данные из ответа мутации без лишнего запроса
  const setData = useCallback((data: T) => setState({ data, error: null }), []);

  return { data: state.data, error: state.error, reload, setData };
}
