'use client';

// «Your care team» — экран пациента (/care, сюда же ведёт регистрация).
//   1. Сверху — решения, которые ждут пациента: врач/лаборатория приняли запрос → «поделиться данными?»;
//   2. текущая команда (активные связи) с кнопкой «Stop sharing»;
//   3. отправленные запросы (можно отозвать);
//   4. предложения по городу: врачи и лаборатории, сначала менее загруженные (порядок задаёт API);
//   5. история (отклонённые / отменённые / завершённые).
// Ничего не передаётся врачу, пока пациент сам не нажмёт «Share my records».

import { useCallback, useMemo, useState, type ReactNode } from 'react';
import Link from 'next/link';
import { Building2, Clock, FlaskConical, Languages, MapPin, Phone, ShieldCheck, Stethoscope, UserRound } from 'lucide-react';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import Modal from '@/components/ui/Modal';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import { CONTROL_CLASS } from '@/components/ui/Field';
import { careApi, getErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { useI18n } from '@/lib/i18n';
import { useApiData } from '@/lib/useApiData';
import { TESTING_CITIES, type CareConnection, type DoctorSuggestion, type LaboratorySuggestion } from '@/types/api';
import { CareStatusBadge, Initials, TextAreaField, useDateFormat } from './shared';

type Tab = 'doctors' | 'laboratories';
type RequestTarget = { kind: 'doctor'; doctor: DoctorSuggestion } | { kind: 'laboratory'; laboratory: LaboratorySuggestion };

export default function PatientCareTeam() {
  const { user } = useAuth();
  const { t } = useI18n();
  const c = t.care;
  const { data: connections, error, reload } = useApiData(careApi.connections);

  const [city, setCity] = useState(user?.city ?? '');
  const loadSuggestions = useCallback(() => (city ? careApi.suggestions(city) : Promise.resolve(null)), [city]);
  const { data: suggestions, error: suggestionsError, reload: reloadSuggestions } = useApiData(loadSuggestions);

  const [tab, setTab] = useState<Tab>('doctors');
  const [target, setTarget] = useState<RequestTarget | null>(null);
  const [toStop, setToStop] = useState<CareConnection | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  const groups = useMemo(() => {
    const list = connections ?? [];
    return {
      decisions: list.filter((item) => item.status === 'Accepted'),
      active: list.filter((item) => item.status === 'Active'),
      pending: list.filter((item) => item.status === 'Requested'),
      closed: list.filter((item) => ['Declined', 'Cancelled', 'Ended'].includes(item.status)),
    };
  }, [connections]);

  const refreshAll = () => {
    reload();
    reloadSuggestions();
  };

  // Действие над связью (подтвердить / отказаться / отозвать) с блокировкой кнопки и показом ошибки
  const run = async (id: number, action: () => Promise<unknown>) => {
    setBusyId(id);
    setActionError(null);
    try {
      await action();
      refreshAll();
    } catch (err) {
      setActionError(getErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  const isFirstVisit = connections !== null && connections.length === 0;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold sm:text-3xl">{c.title}</h1>
        <p className="mt-1 max-w-2xl text-sm text-[#6b756e]">{c.subtitle}</p>
      </div>

      {isFirstVisit && (
        <div className="flex flex-col gap-4 rounded-3xl border border-[#d5e0d1] bg-[#e4ece5] p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
          <div className="max-w-2xl">
            <h2 className="font-serif-aesthetic text-[28px] leading-tight text-[#2a302b]">{c.welcomeTitle}</h2>
            <p className="mt-2 text-sm leading-relaxed text-[#4a524c]">{c.welcomeText}</p>
          </div>
          <Link href="/dashboard" className="shrink-0 text-sm font-medium text-[#3d5340] underline-offset-4 hover:underline">
            {c.skip}
          </Link>
        </div>
      )}

      {error && <Alert>{error}</Alert>}
      {actionError && <Alert>{actionError}</Alert>}
      {notice && <Alert variant="success">{notice}</Alert>}
      {!connections && !error && <LoadingState />}

      {/* Решения, которые ждут пациента */}
      {groups.decisions.map((item) => {
        const isDoctor = item.providerType === 'Doctor';
        return (
          <section key={item.id} className="rounded-3xl border border-[#eadfc8] bg-[#fbf6ec] p-5 sm:p-6" aria-live="polite">
            <div className="text-[11px] font-semibold uppercase tracking-[0.14em] text-[#7a5a22]">{c.actionNeeded}</div>
            <div className="mt-3 flex items-start gap-3">
              <Initials name={item.provider.name} tone="sand" />
              <div className="min-w-0">
                <h2 className="text-base font-semibold text-[#2a302b]">{c.acceptedTitle(item.provider.name)}</h2>
                {item.provider.subtitle && <div className="text-xs text-[#6b756e]">{item.provider.subtitle}</div>}
                <p className="mt-2 text-sm leading-relaxed text-[#4a524c]">
                  {isDoctor ? c.shareDoctorText(item.provider.name) : c.shareLabText(item.provider.name)}
                </p>
              </div>
            </div>
            <div className="mt-4 flex flex-wrap gap-2 sm:pl-14">
              <Button onClick={() => run(item.id, () => careApi.confirm(item.id))} isLoading={busyId === item.id}>
                <ShieldCheck className="h-4 w-4" />
                {isDoctor ? c.share : c.allow}
              </Button>
              <Button variant="secondary" disabled={busyId === item.id} onClick={() => run(item.id, () => careApi.end(item.id))}>
                {c.refuse}
              </Button>
            </div>
          </section>
        );
      })}

      {connections && (
        <Card title={c.current}>
          {groups.active.length === 0 ? (
            <EmptyState>{c.currentEmpty}</EmptyState>
          ) : (
            <ul className="divide-y divide-[#e1e7dc]">
              {groups.active.map((item) => (
                <ConnectionRow key={item.id} connection={item}>
                  <Button variant="ghost" size="sm" onClick={() => setToStop(item)}>{c.stopSharing}</Button>
                </ConnectionRow>
              ))}
            </ul>
          )}
        </Card>
      )}

      {groups.pending.length > 0 && (
        <Card title={c.pending}>
          <ul className="divide-y divide-[#e1e7dc]">
            {groups.pending.map((item) => (
              <ConnectionRow key={item.id} connection={item}>
                <Button variant="ghost" size="sm" isLoading={busyId === item.id} onClick={() => run(item.id, () => careApi.end(item.id))}>
                  {c.withdraw}
                </Button>
              </ConnectionRow>
            ))}
          </ul>
        </Card>
      )}

      {/* Предложения по городу */}
      <Card>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2 className="text-base font-semibold text-[#2a302b]">{c.suggestionsTitle}</h2>
            <p className="mt-1 max-w-xl text-xs leading-relaxed text-[#6b756e]">{city ? c.suggestionsText(city) : c.noCity}</p>
          </div>
          <label className="block sm:w-52">
            <span className="mb-1.5 block text-xs font-medium text-[#6b756e]">{c.city}</span>
            <select value={city} onChange={(event) => setCity(event.target.value)} className={`${CONTROL_CLASS} py-2.5!`}>
              <option value="">{c.chooseCity}</option>
              {TESTING_CITIES.map((name) => <option key={name} value={name}>{name}</option>)}
            </select>
          </label>
        </div>

        {city && (
          <>
            <div className="mt-5 inline-flex rounded-full bg-[#eef0e9] p-1" role="tablist">
              {(['doctors', 'laboratories'] as Tab[]).map((value) => (
                <button
                  key={value}
                  type="button"
                  role="tab"
                  aria-selected={tab === value}
                  onClick={() => setTab(value)}
                  className={`inline-flex items-center gap-1.5 rounded-full px-4 py-1.5 text-sm font-medium transition ${
                    tab === value ? 'bg-white text-[#2a302b] shadow-[0_1px_2px_rgba(42,48,43,0.08)]' : 'text-[#6c736c] hover:text-[#2a302b]'
                  }`}
                >
                  {value === 'doctors' ? <Stethoscope className="h-4 w-4" /> : <FlaskConical className="h-4 w-4" />}
                  {value === 'doctors' ? c.doctors : c.laboratories}
                  {suggestions && (
                    <span className="text-xs text-[#9aa19a]">{value === 'doctors' ? suggestions.doctors.length : suggestions.laboratories.length}</span>
                  )}
                </button>
              ))}
            </div>

            {suggestionsError && <Alert className="mt-4">{suggestionsError}</Alert>}
            {!suggestions && !suggestionsError && <LoadingState />}
            {suggestions && tab === 'doctors' && (
              suggestions.doctors.length === 0 ? <EmptyState>{c.noDoctors}</EmptyState> : (
                <div className="mt-5 grid grid-cols-1 gap-4 md:grid-cols-2">
                  {suggestions.doctors.map((doctor) => (
                    <DoctorCard key={doctor.id} doctor={doctor} onRequest={() => setTarget({ kind: 'doctor', doctor })} />
                  ))}
                </div>
              )
            )}
            {suggestions && tab === 'laboratories' && (
              suggestions.laboratories.length === 0 ? <EmptyState>{c.noLabs}</EmptyState> : (
                <div className="mt-5 grid grid-cols-1 gap-4 md:grid-cols-2">
                  {suggestions.laboratories.map((laboratory) => (
                    <LaboratoryCard key={laboratory.id} laboratory={laboratory} onRequest={() => setTarget({ kind: 'laboratory', laboratory })} />
                  ))}
                </div>
              )
            )}
          </>
        )}
      </Card>

      {groups.closed.length > 0 && (
        <details className="group rounded-3xl border border-[#e1e7dc] bg-white p-5 sm:p-6">
          <summary className="cursor-pointer list-none text-base font-semibold text-[#2a302b] [&::-webkit-details-marker]:hidden">
            {c.history} <span className="text-sm font-normal text-[#8e9890]">({groups.closed.length})</span>
          </summary>
          <ul className="mt-4 divide-y divide-[#e1e7dc]">
            {groups.closed.map((item) => <ConnectionRow key={item.id} connection={item} />)}
          </ul>
        </details>
      )}

      {target && (
        <RequestModal
          target={target}
          onClose={() => setTarget(null)}
          onSent={(name) => {
            setTarget(null);
            setNotice(c.requestSent(name));
            refreshAll();
          }}
        />
      )}

      <ConfirmDialog
        isOpen={toStop !== null}
        title={c.stopTitle}
        message={toStop ? (toStop.providerType === 'Doctor' ? c.stopDoctorText(toStop.provider.name) : c.stopLabText(toStop.provider.name)) : ''}
        confirmLabel={c.stopSharing}
        onClose={() => setToStop(null)}
        onConfirm={async () => {
          if (toStop) {
            await careApi.end(toStop.id);
            refreshAll();
          }
        }}
      />
    </div>
  );
}

// Строка связи: кто, статус, когда, сообщения; справа — кнопки
function ConnectionRow({ connection, children }: { connection: CareConnection; children?: ReactNode }) {
  const { t } = useI18n();
  const c = t.care;
  const formatDate = useDateFormat();
  const isDoctor = connection.providerType === 'Doctor';
  const date = connection.endedAt ?? connection.confirmedAt ?? connection.respondedAt ?? connection.requestedAt;

  return (
    <li className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 items-start gap-3">
        <Initials name={connection.provider.name} />
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-medium text-[#2a302b]">{connection.provider.name}</span>
            <CareStatusBadge status={connection.status} />
          </div>
          <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-[#6b756e]">
            <span className="inline-flex items-center gap-1">
              {isDoctor ? <Stethoscope className="h-3 w-3" /> : <Building2 className="h-3 w-3" />}
              {isDoctor ? c.doctor : c.laboratory}
            </span>
            {connection.provider.subtitle && <span>{connection.provider.subtitle}</span>}
            <span>{formatDate(date)}</span>
          </div>
          {connection.status === 'Active' && (
            <div className="mt-1 text-xs text-[#3d5340]">
              {isDoctor ? c.sharingDoctor : c.sharingLab} · {c.since(formatDate(connection.confirmedAt))}
            </div>
          )}
          {connection.patientMessage && <p className="mt-1.5 text-xs text-[#6b756e]">{c.yourMessage}: “{connection.patientMessage}”</p>}
          {connection.providerNote && <p className="mt-1 text-xs text-[#8a4a23]">{c.note}: “{connection.providerNote}”</p>}
        </div>
      </div>
      {children && <div className="flex shrink-0 gap-2 sm:pl-4">{children}</div>}
    </li>
  );
}

function DoctorCard({ doctor, onRequest }: { doctor: DoctorSuggestion; onRequest: () => void }) {
  const { t } = useI18n();
  const c = t.care;
  return (
    <article className="flex flex-col rounded-[22px] border border-[#e8ebe3] bg-[#fbfbf8] p-4 sm:p-5">
      <div className="flex items-start gap-3">
        <Initials name={doctor.fullName} />
        <div className="min-w-0">
          <h3 className="font-semibold text-[#2a302b]">{doctor.fullName}</h3>
          <div className="text-sm text-[#4a524c]">{doctor.specialty || c.doctor}</div>
          {doctor.workplace && (
            <div className="mt-0.5 flex items-center gap-1 text-xs text-[#6b756e]">
              <MapPin className="h-3 w-3 shrink-0" />
              <span className="truncate">{doctor.workplace} · {doctor.city}</span>
            </div>
          )}
        </div>
      </div>
      {doctor.bio && <p className="mt-3 text-sm leading-relaxed text-[#5b625b]">{doctor.bio}</p>}
      <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-[#6b756e]">
        {doctor.languages && (
          <span className="inline-flex items-center gap-1"><Languages className="h-3 w-3" />{doctor.languages}</span>
        )}
        <span className="inline-flex items-center gap-1"><UserRound className="h-3 w-3" />{c.patientCount(doctor.patientCount)}</span>
      </div>
      <div className="mt-4 flex-1" />
      <Button className="self-start" onClick={onRequest}>{c.request}</Button>
    </article>
  );
}

function LaboratoryCard({ laboratory, onRequest }: { laboratory: LaboratorySuggestion; onRequest: () => void }) {
  const { t } = useI18n();
  const c = t.care;
  return (
    <article className="flex flex-col rounded-[22px] border border-[#e8ebe3] bg-[#fbfbf8] p-4 sm:p-5">
      <div className="flex items-start gap-3">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-[#e4ece5] text-[#3d5340]" aria-hidden>
          <FlaskConical className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <h3 className="font-semibold text-[#2a302b]">{laboratory.name}</h3>
          {laboratory.address && (
            <div className="mt-0.5 flex items-center gap-1 text-xs text-[#6b756e]">
              <MapPin className="h-3 w-3 shrink-0" />
              <span>{laboratory.address} · {laboratory.city}</span>
            </div>
          )}
        </div>
      </div>
      {laboratory.description && <p className="mt-3 text-sm leading-relaxed text-[#5b625b]">{laboratory.description}</p>}
      <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-[#6b756e]">
        {laboratory.openingHours && <span className="inline-flex items-center gap-1"><Clock className="h-3 w-3" />{laboratory.openingHours}</span>}
        {laboratory.phone && <span className="inline-flex items-center gap-1"><Phone className="h-3 w-3" />{laboratory.phone}</span>}
        <span className="inline-flex items-center gap-1"><UserRound className="h-3 w-3" />{c.patientCount(laboratory.patientCount)}</span>
      </div>
      <div className="mt-4 flex-1" />
      <Button className="self-start" onClick={onRequest}>{c.request}</Button>
    </article>
  );
}

function RequestModal({ target, onClose, onSent }: { target: RequestTarget; onClose: () => void; onSent: (name: string) => void }) {
  const { t } = useI18n();
  const c = t.care;
  const [message, setMessage] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSending, setIsSending] = useState(false);
  const name = target.kind === 'doctor' ? target.doctor.fullName : target.laboratory.name;

  const send = async () => {
    setIsSending(true);
    setError(null);
    try {
      await careApi.request(
        target.kind === 'doctor'
          ? { doctorId: target.doctor.id, message: message.trim() || null }
          : { laboratoryId: target.laboratory.id, message: message.trim() || null },
      );
      onSent(name);
    } catch (err) {
      setError(getErrorMessage(err));
      setIsSending(false);
    }
  };

  return (
    <Modal
      isOpen
      title={c.requestTitle(name)}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>{c.cancel}</Button>
          <Button onClick={send} isLoading={isSending}>{c.request}</Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="flex items-start gap-2 rounded-2xl bg-[#f3f6ef] px-4 py-3 text-sm text-[#3d5340]">
          <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" />
          {target.kind === 'doctor' ? c.requestDoctorText : c.requestLabText}
        </p>
        <TextAreaField
          label={c.message}
          id="care-message"
          maxLength={300}
          placeholder={c.messagePlaceholder}
          value={message}
          onChange={(event) => setMessage(event.target.value)}
        />
        {error && <Alert>{error}</Alert>}
      </div>
    </Modal>
  );
}
