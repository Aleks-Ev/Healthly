// Форматирование значений для интерфейса (всегда на английском)

export function formatDate(iso: string | null | undefined): string {
  if (!iso) {
    return '—';
  }
  // Дата без времени ("1990-05-14") парсится как полночь UTC — показываем её в UTC, чтобы не сдвинулся день
  const isDateOnly = /^\d{4}-\d{2}-\d{2}$/.test(iso);
  return new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', timeZone: isDateOnly ? 'UTC' : undefined });
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

// "" → null, "180" → 180 (для необязательных числовых полей форм)
export function toNumberOrNull(value: string): number | null {
  const trimmed = value.trim().replace(',', '.');
  if (trimmed === '') {
    return null;
  }
  const number = Number(trimmed);
  return Number.isFinite(number) ? number : null;
}
