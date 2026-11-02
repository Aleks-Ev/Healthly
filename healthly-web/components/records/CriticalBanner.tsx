// Красное предупреждение о критических значениях (за критическим порогом справочника норм): срочно к врачу.
// Используется в ИИ-анализе, на странице документа и на дашборде.
// uncertain — значение могло быть прочитано с ошибкой (OCR): тогда сначала просим сверить с бланком.

import { AlertTriangle } from 'lucide-react';

export interface CriticalItem {
  testName: string;
  value: string;
  unit: string;
  uncertain?: boolean;
}

export default function CriticalBanner({ items }: { items: CriticalItem[] }) {
  if (items.length === 0) {
    return null;
  }
  const allUncertain = items.every((item) => item.uncertain);
  const plural = items.length > 1 ? 's' : '';

  return (
    <div role="alert" className="flex items-start gap-3 rounded-2xl border-2 border-red-500 bg-red-50 px-4 py-3 text-red-800">
      <AlertTriangle className="w-5 h-5 mt-0.5 shrink-0" />
      <div className="text-sm">
        <p className="font-semibold">
          {allUncertain ? `Possible critical value${plural}` : `Critical value${plural}`}:{' '}
          {items.map((item) => `${item.testName} ${item.value} ${item.unit}`.trim()).join(', ')}
        </p>
        <p className="mt-1">
          {allUncertain
            ? 'This value may also be a reading error. Check it against the original report and, if it is confirmed, contact your doctor right away.'
            : 'Contact your doctor right away or seek urgent medical care.'}
        </p>
      </div>
    </div>
  );
}
