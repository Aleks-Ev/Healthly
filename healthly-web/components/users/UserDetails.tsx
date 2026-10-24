'use client';

// Карточка пользователя для врача/админа: профиль + медкарта (документы, показатели, отчёт).

import { useCallback, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ArrowLeft } from 'lucide-react';
import { LoadingState } from '@/components/ui/Card';
import Alert from '@/components/ui/Alert';
import ProfileCard from './ProfileCard';
import UserFormModal from './UserFormModal';
import PatientRecords from '@/components/records/PatientRecords';
import { usersApi } from '@/lib/api';
import { useApiData } from '@/lib/useApiData';
import { useAuth } from '@/lib/auth';

export default function UserDetails() {
  const params = useParams<{ id: string }>();
  const userId = Number(params.id);
  const { hasRole } = useAuth();
  const isAdmin = hasRole('Admin');

  const [isEditing, setIsEditing] = useState(false);

  const loadUser = useCallback(() => usersApi.get(userId), [userId]);
  const { data: user, error: loadError, setData: setUser } = useApiData(loadUser);

  return (
    <div className="space-y-6">
      <Link href="/admin/users" className="inline-flex items-center gap-1.5 text-sm text-[#6b756e] hover:text-[#242b26]">
        <ArrowLeft className="w-4 h-4" />
        All users
      </Link>

      {loadError && <Alert>{loadError}</Alert>}
      {!loadError && !user && <LoadingState />}

      {user && (
        <>
          <h1 className="text-2xl sm:text-3xl font-semibold">{user.fullName}</h1>
          <div className="grid grid-cols-1 lg:grid-cols-[320px_1fr] gap-6 items-start">
            <ProfileCard user={user} onEdit={isAdmin ? () => setIsEditing(true) : undefined} />
            <PatientRecords userId={user.id} />
          </div>
          <UserFormModal isOpen={isEditing} user={user} isAdmin={isAdmin} onClose={() => setIsEditing(false)} onSaved={setUser} />
        </>
      )}
    </div>
  );
}
