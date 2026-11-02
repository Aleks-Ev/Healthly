'use client';

// Рабочее место сотрудника лаборатории (роль LabStaff):
//   1. пациент называет Healthly ID или показывает QR-код — сканер QR «печатает» код в поле как клавиатура (+ Enter);
//   2. сотрудник сверяет имя и дату рождения, выбирает файл бланка на компьютере лаборатории и загружает его;
//   3. очередь лаборатории: документы «не проанализирован / черновик» → открыть, запустить ИИ-анализ, сверить, подтвердить.
// Сотрудник видит только документы своей лаборатории (это проверяет API).

import { useCallback, useRef, useState, type FormEvent } from 'react';
import Link from 'next/link';
import { FileUp, Search, UserCheck, X } from 'lucide-react';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import Badge, { CriticalBadge } from '@/components/ui/Badge';
import { TextField } from '@/components/ui/Field';
import { getErrorMessage, labApi } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { formatDate } from '@/lib/format';
import { useApiData } from '@/lib/useApiData';
import type { LabAnalysisStatus, LabPatient, MedicalDocumentDetails } from '@/types/api';

const STATUS_BADGES: Record<LabAnalysisStatus, { label: string; tone: 'neutral' | 'amber' | 'green' }> = {
  NotAnalyzed: { label: 'Not analyzed', tone: 'neutral' },
  Draft: { label: 'Waiting for confirmation', tone: 'amber' },
  Confirmed: { label: 'Confirmed', tone: 'green' },
};

export default function LabWorkspace() {
  const { user } = useAuth();
  const [pendingOnly, setPendingOnly] = useState(true);
  const loadQueue = useCallback(() => labApi.documents(pendingOnly), [pendingOnly]);
  const { data: queue, error: queueError, reload } = useApiData(loadQueue);

  if (!user) {
    return null;
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl sm:text-3xl font-semibold">Laboratory workspace</h1>
        <p className="mt-1 text-sm text-[#6b756e]">
          {user.laboratoryName ?? 'No laboratory assigned'} · Find the patient by Healthly ID, upload the lab report and confirm the values.
        </p>
      </div>

      {!user.laboratoryId ? (
        <Alert>Your account is not linked to a laboratory yet. Ask an administrator to assign one.</Alert>
      ) : (
        <UploadCard onUploaded={reload} />
      )}

      <Card
        title="Laboratory documents"
        actions={
          <div className="flex rounded-full bg-[#ebefe6] p-0.5 text-xs">
            {[true, false].map((value) => (
              <button
                key={String(value)}
                onClick={() => setPendingOnly(value)}
                className={`rounded-full px-3 py-1 transition ${pendingOnly === value ? 'bg-white font-medium shadow-sm' : 'text-[#6b756e]'}`}
              >
                {value ? 'To confirm' : 'All'}
              </button>
            ))}
          </div>
        }
      >
        {queueError && <Alert>{queueError}</Alert>}
        {!queue && !queueError && <LoadingState label="Loading documents…" />}
        {queue && queue.length === 0 && (
          <EmptyState>{pendingOnly ? 'Nothing to confirm. Upload a report to start.' : 'No documents uploaded by this laboratory yet.'}</EmptyState>
        )}
        {queue && queue.length > 0 && (
          <div className="overflow-x-auto -mx-5 sm:-mx-6">
            <table className="w-full min-w-[720px] text-sm">
              <thead>
                <tr className="text-left text-xs text-[#6b756e] border-b border-[#e1e7dc]">
                  <th className="font-medium px-5 sm:px-6 py-2">Patient</th>
                  <th className="font-medium px-3 py-2">File</th>
                  <th className="font-medium px-3 py-2">Uploaded</th>
                  <th className="font-medium px-3 py-2">Status</th>
                  <th className="font-medium px-5 sm:px-6 py-2 text-right" />
                </tr>
              </thead>
              <tbody>
                {queue.map((document) => (
                  <tr key={document.id} className={`border-b border-[#e1e7dc] last:border-0 ${document.criticalCount > 0 ? 'bg-red-50' : ''}`}>
                    <td className="px-5 sm:px-6 py-3">
                      <div className="font-medium">{document.patientName}</div>
                      <div className="text-xs text-[#6b756e] font-mono">{document.patientCode}</div>
                    </td>
                    <td className="px-3 py-3 break-all">{document.originalFileName}</td>
                    <td className="px-3 py-3 text-[#6b756e]">
                      {formatDate(document.uploadDate)}
                      {document.uploadedBy && <span className="block text-xs">by {document.uploadedBy}</span>}
                    </td>
                    <td className="px-3 py-3">
                      <div className="flex flex-wrap gap-1">
                        <Badge tone={STATUS_BADGES[document.analysisStatus].tone}>{STATUS_BADGES[document.analysisStatus].label}</Badge>
                        {document.criticalCount > 0 && <CriticalBadge />}
                      </div>
                    </td>
                    <td className="px-5 sm:px-6 py-3 text-right">
                      <Link href={`/documents/${document.id}`} className="text-sm font-medium text-[#3a5235] hover:underline">
                        {document.analysisStatus === 'Confirmed' ? 'Open' : document.analysisStatus === 'Draft' ? 'Check & confirm' : 'Analyze'}
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

// Поиск пациента по Healthly ID и загрузка файла с компьютера лаборатории
function UploadCard({ onUploaded }: { onUploaded: () => void }) {
  const [code, setCode] = useState('');
  const [patient, setPatient] = useState<LabPatient | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [uploaded, setUploaded] = useState<MedicalDocumentDetails | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSearching, setIsSearching] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const reset = () => {
    setPatient(null);
    setFile(null);
    setCode('');
    setError(null);
  };

  const handleSearch = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setUploaded(null);
    setIsSearching(true);
    try {
      setPatient(await labApi.findPatient(code));
    } catch (err) {
      setPatient(null);
      setError(getErrorMessage(err));
    } finally {
      setIsSearching(false);
    }
  };

  const handleUpload = async () => {
    if (!patient || !file) {
      return;
    }
    setError(null);
    setIsUploading(true);
    try {
      setUploaded(await labApi.upload(patient.patientCode, file));
      reset();
      onUploaded();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <Card title="Upload a lab report">
      {!patient ? (
        <form onSubmit={handleSearch} className="flex flex-col sm:flex-row sm:items-end gap-3">
          <div className="flex-1">
            <TextField
              label="Patient's Healthly ID"
              id="lab-patient-code"
              placeholder="HL-XXXXXX"
              hint="Ask the patient for their Healthly ID or scan the QR code from their app"
              autoFocus
              autoComplete="off"
              value={code}
              onChange={(event) => setCode(event.target.value)}
            />
          </div>
          <Button type="submit" isLoading={isSearching} disabled={!code.trim()} className="sm:mb-6">
            <Search className="w-4 h-4" />
            Find patient
          </Button>
        </form>
      ) : (
        <div className="space-y-4">
          <div className="flex items-start justify-between gap-3 rounded-2xl bg-[#f3f6ef] px-4 py-3">
            <div className="flex items-start gap-3">
              <UserCheck className="w-5 h-5 mt-0.5 text-[#3a5235]" />
              <div className="text-sm">
                <div className="font-semibold">{patient.fullName}</div>
                <div className="text-[#6b756e]">
                  <span className="font-mono">{patient.patientCode}</span>
                  {' · '}Born {patient.dateOfBirth ? formatDate(patient.dateOfBirth) : 'not provided'}
                  {patient.gender && ` · ${patient.gender}`}
                </div>
                <div className="mt-1 text-xs text-[#6b756e]">Check the name and date of birth against the lab report before uploading.</div>
              </div>
            </div>
            <Button variant="ghost" size="sm" aria-label="Choose another patient" onClick={reset}>
              <X className="w-4 h-4" />
            </Button>
          </div>

          <div className="flex flex-col sm:flex-row sm:items-center gap-3">
            <input
              ref={fileInput}
              type="file"
              accept=".pdf,.png,.jpg,.jpeg"
              className="hidden"
              onChange={(event) => setFile(event.target.files?.[0] ?? null)}
            />
            <Button variant="secondary" onClick={() => fileInput.current?.click()}>
              <FileUp className="w-4 h-4" />
              {file ? 'Choose another file' : 'Choose file'}
            </Button>
            <span className="text-sm text-[#6b756e] break-all">{file ? file.name : 'PDF, PNG or JPG, up to 10 MB'}</span>
            <Button className="sm:ml-auto" onClick={handleUpload} disabled={!file} isLoading={isUploading}>
              Upload
            </Button>
          </div>
        </div>
      )}

      {error && <Alert className="mt-4">{error}</Alert>}
      {uploaded && (
        <Alert variant="success" className="mt-4">
          {uploaded.originalFileName} uploaded.{' '}
          <Link href={`/documents/${uploaded.id}`} className="font-semibold underline">Open it to analyze and confirm</Link>
        </Alert>
      )}
    </Card>
  );
}
