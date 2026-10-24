import { Suspense } from 'react';
import AppShell from '@/components/layout/AppShell';
import UserDetails from '@/components/users/UserDetails';
import { LoadingState } from '@/components/ui/Card';

// Серверная обёртка: UserDetails читает [id] через useParams (нужен Suspense при cacheComponents)
export default function UserDetailsPage() {
  return (
    <AppShell roles={['Doctor', 'Admin']}>
      <Suspense fallback={<LoadingState />}>
        <UserDetails />
      </Suspense>
    </AppShell>
  );
}
