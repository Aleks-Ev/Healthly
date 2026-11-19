'use client';

// Запросы пациентов глазами врача (/care), лаборатории (блок на /lab) и админа (/care, только просмотр).
//   • New requests — принять или отклонить (с необязательной запиской пациенту);
//   • Accepted — ждём, пока пациент подтвердит;
//   • My patients / Connected patients — активные связи: врач открывает медкарту, лаборатория загружает бланк по Healthly ID;
//   • Closed — история.
// До подтверждения пациентом врач видит только имя, возраст, пол, город и сообщение — никаких медданных.

import { useMemo, useState, type ReactNode } from 'react';
import Link from 'next/link';
import { Check, FileUp, FolderOpen, X } from 'lucide-react';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import Modal from '@/components/ui/Modal';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import { careApi, getErrorMessage } from '@/lib/api';
import { useI18n } from '@/lib/i18n';
import { useApiData } from '@/lib/useApiData';
import type { CareConnection } from '@/types/api';
import { CareStatusBadge, Initials, TextAreaField, useDateFormat } from './shared';

type Mode = 'doctor' | 'lab' | 'admin';

interface ProviderRequestsProps {
  mode: Mode;
  // Лаборатория: загрузить бланк выбранному пациенту (передаётся Healthly ID)
  onUpload?: (patientCode: string) => void;
  // Показывать заголовок страницы (на /lab блок встроен без него)
  showHeading?: boolean;
}

export default function ProviderRequests({ mode, onUpload, showHeading = true }: ProviderRequestsProps) {
  const { t } = useI18n();
  const p = t.provider;
  const { data: connections, error, reload } = useApiData(careApi.connections);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [toDecline, setToDecline] = useState<CareConnection | null>(null);
  const [toEnd, setToEnd] = useState<CareConnection | null>(null);
  const canAct = mode !== 'admin';

  const groups = useMemo(() => {
    const list = connections ?? [];
    return {
      requested: list.filter((item) => item.status === 'Requested'),
      accepted: list.filter((item) => item.status === 'Accepted'),
      active: list.filter((item) => item.status === 'Active'),
      closed: list.filter((item) => ['Declined', 'Cancelled', 'Ended'].includes(item.status)),
    };
  }, [connections]);

  const accept = async (id: number) => {
    setBusyId(id);
    setActionError(null);
    try {
      await careApi.accept(id);
      reload();
    } catch (err) {
      setActionError(getErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  const title = mode === 'doctor' ? p.doctorTitle : mode === 'lab' ? p.labTitle : p.adminTitle;
  const subtitle = mode === 'doctor' ? p.doctorSubtitle : mode === 'lab' ? p.labSubtitle : p.adminSubtitle;

  return (
    <div className="space-y-6">
      {showHeading && (
        <div>
          <h1 className="text-2xl font-semibold sm:text-3xl">{title}</h1>
          <p className="mt-1 max-w-2xl text-sm text-[#6b756e]">{subtitle}</p>
        </div>
      )}

      {error && <Alert>{error}</Alert>}
      {actionError && <Alert>{actionError}</Alert>}
      {!connections && !error && <LoadingState />}

      {connections && (
        <Card title={`${p.newRequests}${groups.requested.length ? ` · ${groups.requested.length}` : ''}`}>
          {!showHeading && <p className="-mt-2 mb-4 text-xs text-[#8e9890]">{subtitle}</p>}
          {groups.requested.length === 0 ? (
            <EmptyState>{p.noRequests}</EmptyState>
          ) : (
            <ul className="divide-y divide-[#e1e7dc]">
              {groups.requested.map((item) => (
                <PatientRow key={item.id} connection={item} showProvider={mode === 'admin'}>
                  {canAct && (
                    <>
                      <Button size="sm" onClick={() => accept(item.id)} isLoading={busyId === item.id}>
                        <Check className="h-3.5 w-3.5" />
                        {p.accept}
                      </Button>
                      <Button size="sm" variant="secondary" disabled={busyId === item.id} onClick={() => setToDecline(item)}>
                        <X className="h-3.5 w-3.5" />
                        {p.decline}
                      </Button>
                    </>
                  )}
                </PatientRow>
              ))}
            </ul>
          )}
        </Card>
      )}

      {groups.accepted.length > 0 && (
        <Card title={p.waitingPatient}>
          <ul className="divide-y divide-[#e1e7dc]">
            {groups.accepted.map((item) => (
              <PatientRow key={item.id} connection={item} showProvider={mode === 'admin'}>
                {canAct && <Button size="sm" variant="ghost" onClick={() => setToEnd(item)}>{p.end}</Button>}
              </PatientRow>
            ))}
          </ul>
        </Card>
      )}

      {connections && (
        <Card title={mode === 'lab' ? p.connectedPatients : p.myPatients}>
          {groups.active.length === 0 ? (
            <EmptyState>{p.noConnected}</EmptyState>
          ) : (
            <ul className="divide-y divide-[#e1e7dc]">
              {groups.active.map((item) => (
                <PatientRow key={item.id} connection={item} showProvider={mode === 'admin'}>
                  {mode === 'doctor' && (
                    <Link
                      href={`/admin/users/${item.patient.id}`}
                      className="inline-flex items-center gap-1.5 rounded-full bg-[#737f69] px-3 py-1.5 text-xs font-medium text-white transition hover:bg-[#5f6b57]"
                    >
                      <FolderOpen className="h-3.5 w-3.5" />
                      {p.openRecords}
                    </Link>
                  )}
                  {mode === 'lab' && item.patient.patientCode && onUpload && (
                    <Button size="sm" onClick={() => onUpload(item.patient.patientCode!)}>
                      <FileUp className="h-3.5 w-3.5" />
                      {p.uploadReport}
                    </Button>
                  )}
                  {canAct && <Button size="sm" variant="ghost" onClick={() => setToEnd(item)}>{p.end}</Button>}
                </PatientRow>
              ))}
            </ul>
          )}
        </Card>
      )}

      {groups.closed.length > 0 && (
        <details className="rounded-3xl border border-[#e1e7dc] bg-white p-5 sm:p-6">
          <summary className="cursor-pointer list-none text-base font-semibold text-[#2a302b] [&::-webkit-details-marker]:hidden">
            {p.history} <span className="text-sm font-normal text-[#8e9890]">({groups.closed.length})</span>
          </summary>
          <ul className="mt-4 divide-y divide-[#e1e7dc]">
            {groups.closed.map((item) => <PatientRow key={item.id} connection={item} showProvider={mode === 'admin'} />)}
          </ul>
        </details>
      )}

      {toDecline && <DeclineModal connection={toDecline} onClose={() => setToDecline(null)} onDone={() => { setToDecline(null); reload(); }} />}

      <ConfirmDialog
        isOpen={toEnd !== null}
        title={p.endTitle}
        message={toEnd ? p.endText(toEnd.patient.fullName) : ''}
        confirmLabel={p.end}
        onClose={() => setToEnd(null)}
        onConfirm={async () => {
          if (toEnd) {
            await careApi.end(toEnd.id);
            reload();
          }
        }}
      />
    </div>
  );
}

// Пациент: имя, возраст · пол · город, Healthly ID (только при активной связи), сообщение, статус; справа — кнопки
function PatientRow({ connection, showProvider, children }: { connection: CareConnection; showProvider: boolean; children?: ReactNode }) {
  const { t } = useI18n();
  const p = t.provider;
  const formatDate = useDateFormat();
  const patient = connection.patient;
  const facts = [
    patient.age !== null ? p.age(patient.age) : null,
    patient.gender ? (t.auth.register.genders[patient.gender] ?? patient.gender) : null,
    patient.city || null,
  ].filter(Boolean);

  return (
    <li className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 items-start gap-3">
        <Initials name={patient.fullName} />
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-medium text-[#2a302b]">{patient.fullName}</span>
            <CareStatusBadge status={connection.status} perspective="provider" />
          </div>
          <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-[#6b756e]">
            {facts.length > 0 && <span>{facts.join(' · ')}</span>}
            {patient.patientCode && <span className="font-mono">{patient.patientCode}</span>}
            <span>{p.requested(formatDate(connection.requestedAt))}</span>
          </div>
          {showProvider && <div className="mt-0.5 text-xs text-[#4a524c]">→ {connection.provider.name}</div>}
          {connection.patientMessage && <p className="mt-1.5 text-sm text-[#4a524c]">“{connection.patientMessage}”</p>}
          {connection.providerNote && <p className="mt-1 text-xs text-[#8a4a23]">{t.care.note}: “{connection.providerNote}”</p>}
        </div>
      </div>
      {children && <div className="flex shrink-0 flex-wrap gap-2 sm:justify-end sm:pl-4">{children}</div>}
    </li>
  );
}

function DeclineModal({ connection, onClose, onDone }: { connection: CareConnection; onClose: () => void; onDone: () => void }) {
  const { t } = useI18n();
  const p = t.provider;
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const decline = async () => {
    setIsSaving(true);
    setError(null);
    try {
      await careApi.decline(connection.id, note.trim());
      onDone();
    } catch (err) {
      setError(getErrorMessage(err));
      setIsSaving(false);
    }
  };

  return (
    <Modal
      isOpen
      title={`${p.declineTitle} — ${connection.patient.fullName}`}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{t.care.cancel}</Button>
          <Button variant="danger" onClick={decline} isLoading={isSaving}>{p.decline}</Button>
        </>
      }
    >
      <div className="space-y-4">
        <TextAreaField
          label={p.declineNote}
          id="decline-note"
          maxLength={300}
          placeholder={p.declinePlaceholder}
          value={note}
          onChange={(event) => setNote(event.target.value)}
        />
        {error && <Alert>{error}</Alert>}
      </div>
    </Modal>
  );
}
