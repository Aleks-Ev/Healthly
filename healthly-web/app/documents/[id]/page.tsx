import { Suspense } from 'react';
import AppShell from '@/components/layout/AppShell';
import DocumentDetails from '@/components/records/DocumentDetails';
import { LoadingState } from '@/components/ui/Card';

// Серверная страница-обёртка: DocumentDetails читает [id] через useParams,
// а при cacheComponents такой компонент должен быть внутри Suspense.
export default function DocumentPage() {
  return (
    <AppShell>
      <Suspense fallback={<LoadingState />}>
        <DocumentDetails />
      </Suspense>
    </AppShell>
  );
}
