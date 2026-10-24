'use client';

// Медкарта пользователя: сводка отчёта, динамика показателей, последние показатели и список документов с загрузкой.
// Используется в /dashboard (свои данные) и /admin/users/[id] (данные пациента для врача/админа).

import { useCallback, useRef, useState, type ChangeEvent } from 'react';
import Link from 'next/link';
import { Download, FileText, Trash2, Upload } from 'lucide-react';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import { AbnormalBadge, StatusBadge } from '@/components/ui/Badge';
import HealthTrends from '@/components/records/HealthTrends';
import { documentsApi, getErrorMessage, usersApi } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { formatDate, formatFileSize } from '@/lib/format';
import { useApiData } from '@/lib/useApiData';
import type { MedicalDocument } from '@/types/api';

const ACCEPTED_FILES = '.pdf,.png,.jpg,.jpeg';

export default function PatientRecords({ userId }: { userId: number }) {
  const { user, hasRole } = useAuth();
  const [actionError, setActionError] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [documentToDelete, setDocumentToDelete] = useState<MedicalDocument | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  // Удалять документ может владелец или Admin (так же проверяет API)
  const canDelete = user?.id === userId || hasRole('Admin');

  // Отчёт, динамика и список документов загружаются параллельно
  const loadRecords = useCallback(async () => {
    const [report, trends, documents] = await Promise.all([
      usersApi.report(userId),
      usersApi.trends(userId),
      documentsApi.listForUser(userId),
    ]);
    return { report, trends, documents };
  }, [userId]);
  const { data, error: loadError, reload } = useApiData(loadRecords);

  const handleFileSelected = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = ''; // чтобы можно было выбрать тот же файл ещё раз
    if (!file) {
      return;
    }
    setActionError(null);
    setIsUploading(true);
    try {
      await documentsApi.upload(userId, file);
      reload();
    } catch (err) {
      setActionError(getErrorMessage(err));
    } finally {
      setIsUploading(false);
    }
  };

  const handleDownload = async (document: MedicalDocument) => {
    setActionError(null);
    try {
      await documentsApi.download(document.id, document.originalFileName);
    } catch (err) {
      setActionError(getErrorMessage(err));
    }
  };

  if (loadError) {
    return <Alert>{loadError}</Alert>;
  }

  if (!data) {
    return <LoadingState label="Loading health records…" />;
  }

  const { report, trends, documents } = data;

  const stats = [
    { label: 'Documents', value: report.documentsCount },
    { label: 'Test results', value: report.resultsCount },
    { label: 'Out of range', value: report.abnormalCount, highlight: report.abnormalCount > 0 },
    { label: 'Last upload', value: formatDate(report.lastUploadDate) },
  ];

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {stats.map((stat) => (
          <div key={stat.label} className="rounded-2xl bg-white border border-[#e1e7dc] p-4">
            <div className="text-xs text-[#6b756e]">{stat.label}</div>
            <div className={`mt-1 text-2xl font-semibold ${stat.highlight ? 'text-red-600' : 'text-[#242b26]'}`}>{stat.value}</div>
          </div>
        ))}
      </div>

      <HealthTrends trends={trends} onUpload={() => fileInput.current?.click()} isUploading={isUploading} />

      <Card title="Latest indicators">
        {report.indicators.length === 0 ? (
          <EmptyState>No test results yet. Upload a document and add its results.</EmptyState>
        ) : (
          <div className="overflow-x-auto -mx-5 sm:-mx-6">
            <table className="w-full min-w-[560px] text-sm">
              <thead>
                <tr className="text-left text-xs text-[#6b756e] border-b border-[#e1e7dc]">
                  <th className="font-medium px-5 sm:px-6 py-2">Test</th>
                  <th className="font-medium px-3 py-2">Latest value</th>
                  <th className="font-medium px-3 py-2">Reference range</th>
                  <th className="font-medium px-3 py-2">Status</th>
                  <th className="font-medium px-5 sm:px-6 py-2 text-right">Measured</th>
                </tr>
              </thead>
              <tbody>
                {report.indicators.map((indicator) => (
                  <tr key={indicator.testName} className="border-b border-[#e1e7dc] last:border-0">
                    <td className="px-5 sm:px-6 py-3 font-medium">
                      <Link href={`/documents/${indicator.documentId}`} className="hover:text-[#72826c]">{indicator.testName}</Link>
                    </td>
                    <td className="px-3 py-3">{indicator.latestValue} <span className="text-[#6b756e]">{indicator.unit}</span></td>
                    <td className="px-3 py-3 text-[#6b756e]">{indicator.referenceRange || '—'}</td>
                    <td className="px-3 py-3"><AbnormalBadge isAbnormal={indicator.isAbnormal} /></td>
                    <td className="px-5 sm:px-6 py-3 text-right text-[#6b756e]">
                      {formatDate(indicator.measuredAt)}
                      {indicator.measurementsCount > 1 && <span className="block text-xs">{indicator.measurementsCount} measurements</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <Card
        title="Documents"
        actions={
          <>
            <input ref={fileInput} type="file" accept={ACCEPTED_FILES} className="hidden" onChange={handleFileSelected} />
            <Button size="sm" onClick={() => fileInput.current?.click()} isLoading={isUploading}>
              {!isUploading && <Upload className="w-3.5 h-3.5" />}
              Upload document
            </Button>
          </>
        }
      >
        <p className="-mt-2 mb-4 text-xs text-[#8e9890]">PDF, PNG or JPG, up to 10 MB.</p>
        {actionError && <Alert className="mb-4">{actionError}</Alert>}

        {documents.length === 0 ? (
          <EmptyState>No documents uploaded yet.</EmptyState>
        ) : (
          <ul className="divide-y divide-[#e1e7dc]">
            {documents.map((document) => (
              <li key={document.id} className="flex flex-wrap items-center gap-3 py-3">
                <FileText className="w-5 h-5 text-[#72826c] shrink-0" />
                <div className="min-w-0 flex-1">
                  <Link href={`/documents/${document.id}`} className="block truncate font-medium hover:text-[#72826c]">
                    {document.originalFileName}
                  </Link>
                  <div className="text-xs text-[#6b756e]">
                    {formatDate(document.uploadDate)} · {formatFileSize(document.fileSizeBytes)} · {document.resultsCount} results
                    {document.abnormalCount > 0 && <span className="text-red-600"> · {document.abnormalCount} out of range</span>}
                  </div>
                </div>
                <StatusBadge status={document.status} />
                <div className="flex items-center gap-1">
                  <Button variant="ghost" size="sm" aria-label="Download" onClick={() => handleDownload(document)}>
                    <Download className="w-4 h-4" />
                  </Button>
                  {canDelete && (
                    <Button variant="ghost" size="sm" aria-label="Delete" onClick={() => setDocumentToDelete(document)}>
                      <Trash2 className="w-4 h-4" />
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <ConfirmDialog
        isOpen={documentToDelete !== null}
        title="Delete document"
        message={`Delete "${documentToDelete?.originalFileName}" and all its test results? This cannot be undone.`}
        onClose={() => setDocumentToDelete(null)}
        onConfirm={async () => {
          if (documentToDelete) {
            await documentsApi.remove(documentToDelete.id);
            reload();
          }
        }}
      />
    </div>
  );
}
