'use client';

// Страница документа: файл, статус обработки и таблица показателей с добавлением/редактированием/удалением.

import { useCallback, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Download, Pencil, Plus, Trash2 } from 'lucide-react';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import { AbnormalBadge, StatusBadge } from '@/components/ui/Badge';
import TestResultFormModal from './TestResultFormModal';
import { documentsApi, getErrorMessage, resultsApi } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { formatDate, formatFileSize } from '@/lib/format';
import { useApiData } from '@/lib/useApiData';
import { DOCUMENT_STATUSES, type DocumentStatus, type TestResult } from '@/types/api';

export default function DocumentDetails() {
  const params = useParams<{ id: string }>();
  const documentId = Number(params.id);
  const router = useRouter();
  const { user, hasRole } = useAuth();

  const loadDocument = useCallback(() => documentsApi.get(documentId), [documentId]);
  const { data: document, error: loadError, reload, setData: setDocument } = useApiData(loadDocument);
  const [actionError, setActionError] = useState<string | null>(null);
  // undefined — окно закрыто, null — добавление, TestResult — редактирование
  const [editingResult, setEditingResult] = useState<TestResult | null | undefined>(undefined);
  const [resultToDelete, setResultToDelete] = useState<TestResult | null>(null);
  const [isDeletingDocument, setIsDeletingDocument] = useState(false);

  if (loadError) {
    return <Alert>{loadError}</Alert>;
  }

  if (!document || !user) {
    return <LoadingState label="Loading document…" />;
  }

  const isOwner = document.userId === user.id;
  const isStaff = hasRole('Doctor', 'Admin');
  const canDeleteDocument = isOwner || hasRole('Admin');
  const backHref = isOwner ? '/dashboard' : `/admin/users/${document.userId}`;

  const handleStatusChange = async (status: DocumentStatus) => {
    setActionError(null);
    try {
      setDocument(await documentsApi.updateStatus(document.id, status));
    } catch (err) {
      setActionError(getErrorMessage(err));
    }
  };

  const handleDownload = async () => {
    setActionError(null);
    try {
      await documentsApi.download(document.id, document.originalFileName);
    } catch (err) {
      setActionError(getErrorMessage(err));
    }
  };

  return (
    <div className="space-y-6">
      <Link href={backHref} className="inline-flex items-center gap-1.5 text-sm text-[#6b756e] hover:text-[#242b26]">
        <ArrowLeft className="w-4 h-4" />
        Back to records
      </Link>

      <Card>
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="text-xl font-semibold break-all">{document.originalFileName}</h1>
            <p className="mt-1 text-sm text-[#6b756e]">
              Uploaded {formatDate(document.uploadDate)} · {formatFileSize(document.fileSizeBytes)}
            </p>
            <div className="mt-3 flex items-center gap-2">
              {isStaff ? (
                <select
                  aria-label="Document status"
                  value={document.status}
                  onChange={(event) => handleStatusChange(event.target.value as DocumentStatus)}
                  className="rounded-full border border-[#e1e7dc] bg-white px-3 py-1 text-xs font-medium outline-none focus:border-[#72826c]"
                >
                  {DOCUMENT_STATUSES.map((status) => (
                    <option key={status} value={status}>{status}</option>
                  ))}
                </select>
              ) : (
                <StatusBadge status={document.status} />
              )}
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" size="sm" onClick={handleDownload}>
              <Download className="w-3.5 h-3.5" />
              Download
            </Button>
            {canDeleteDocument && (
              <Button variant="secondary" size="sm" onClick={() => setIsDeletingDocument(true)}>
                <Trash2 className="w-3.5 h-3.5" />
                Delete
              </Button>
            )}
          </div>
        </div>
        {actionError && <Alert className="mt-4">{actionError}</Alert>}
      </Card>

      <Card
        title="Test results"
        actions={
          <Button size="sm" onClick={() => setEditingResult(null)}>
            <Plus className="w-3.5 h-3.5" />
            Add result
          </Button>
        }
      >
        {document.results.length === 0 ? (
          <EmptyState>No results yet. Add the values from this document.</EmptyState>
        ) : (
          <div className="overflow-x-auto -mx-5 sm:-mx-6">
            <table className="w-full min-w-[560px] text-sm">
              <thead>
                <tr className="text-left text-xs text-[#6b756e] border-b border-[#e1e7dc]">
                  <th className="font-medium px-5 sm:px-6 py-2">Test</th>
                  <th className="font-medium px-3 py-2">Value</th>
                  <th className="font-medium px-3 py-2">Reference range</th>
                  <th className="font-medium px-3 py-2">Status</th>
                  <th className="font-medium px-5 sm:px-6 py-2 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {document.results.map((result) => (
                  <tr key={result.id} className="border-b border-[#e1e7dc] last:border-0">
                    <td className="px-5 sm:px-6 py-3 font-medium">{result.testName}</td>
                    <td className="px-3 py-3">{result.value} <span className="text-[#6b756e]">{result.unit}</span></td>
                    <td className="px-3 py-3 text-[#6b756e]">{result.referenceRange || '—'}</td>
                    <td className="px-3 py-3"><AbnormalBadge isAbnormal={result.isAbnormal} /></td>
                    <td className="px-5 sm:px-6 py-3">
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="sm" aria-label="Edit" onClick={() => setEditingResult(result)}>
                          <Pencil className="w-4 h-4" />
                        </Button>
                        <Button variant="ghost" size="sm" aria-label="Delete" onClick={() => setResultToDelete(result)}>
                          <Trash2 className="w-4 h-4" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <TestResultFormModal
        isOpen={editingResult !== undefined}
        documentId={document.id}
        result={editingResult ?? null}
        onClose={() => setEditingResult(undefined)}
        onSaved={reload}
      />

      <ConfirmDialog
        isOpen={resultToDelete !== null}
        title="Delete test result"
        message={`Delete "${resultToDelete?.testName}"?`}
        onClose={() => setResultToDelete(null)}
        onConfirm={async () => {
          if (resultToDelete) {
            await resultsApi.remove(resultToDelete.id);
            reload();
          }
        }}
      />

      <ConfirmDialog
        isOpen={isDeletingDocument}
        title="Delete document"
        message="Delete this document and all its test results? This cannot be undone."
        onClose={() => setIsDeletingDocument(false)}
        onConfirm={async () => {
          await documentsApi.remove(document.id);
          router.push(backHref);
        }}
      />
    </div>
  );
}
