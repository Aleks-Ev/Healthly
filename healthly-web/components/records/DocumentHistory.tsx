'use client';

// История документа (журнал действий): кто загрузил и из какой лаборатории, кто запускал ИИ-анализ, кто подтвердил.
// Только чтение; записи создаёт API (AuditLog) вместе с самими действиями.

import { useCallback } from 'react';
import { CheckCircle2, FileUp, Sparkles, Trash2, type LucideIcon } from 'lucide-react';
import Card, { LoadingState } from '@/components/ui/Card';
import Alert from '@/components/ui/Alert';
import { documentsApi } from '@/lib/api';
import { useApiData } from '@/lib/useApiData';
import type { AuditEvent } from '@/types/api';

const ACTIONS: Record<AuditEvent['action'], { label: string; icon: LucideIcon }> = {
  PatientLookedUp: { label: 'Patient found', icon: CheckCircle2 },
  DocumentUploaded: { label: 'Uploaded', icon: FileUp },
  DocumentAnalyzed: { label: 'Analyzed with AI', icon: Sparkles },
  AnalysisConfirmed: { label: 'Values confirmed', icon: CheckCircle2 },
  DocumentDeleted: { label: 'Deleted', icon: Trash2 },
};

const dateTime = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' });

// version — меняется после действий на странице (анализ, подтверждение), чтобы история перезагрузилась
export default function DocumentHistory({ documentId, version }: { documentId: number; version: number }) {
  // version в зависимостях — новая функция загрузки → useApiData загружает заново
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const load = useCallback(() => documentsApi.history(documentId), [documentId, version]);
  const { data: events, error } = useApiData(load);

  return (
    <Card title="History">
      {error && <Alert>{error}</Alert>}
      {!events && !error && <LoadingState label="Loading history…" />}
      {events && events.length === 0 && <p className="text-sm text-[#6b756e]">No recorded actions yet.</p>}
      {events && events.length > 0 && (
        <ol className="space-y-3">
          {events.map((event, index) => {
            const { label, icon: Icon } = ACTIONS[event.action] ?? { label: event.action, icon: CheckCircle2 };
            return (
              <li key={index} className="flex items-start gap-3 text-sm">
                <Icon className="w-4 h-4 mt-0.5 text-[#72826c] shrink-0" />
                <div>
                  <span className="font-medium">{label}</span>
                  {' · '}
                  {event.userName || 'System'}
                  {event.laboratoryName && <span className="text-[#6b756e]"> ({event.laboratoryName})</span>}
                  <div className="text-xs text-[#6b756e]">
                    {dateTime.format(new Date(event.at))}
                    {event.details && ` · ${event.details}`}
                  </div>
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </Card>
  );
}
