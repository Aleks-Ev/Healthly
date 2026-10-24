'use client';

import { useState } from 'react';
import AppShell from '@/components/layout/AppShell';
import ProfileCard from '@/components/users/ProfileCard';
import UserFormModal from '@/components/users/UserFormModal';
import ChangePasswordModal from '@/components/users/ChangePasswordModal';
import PatientRecords from '@/components/records/PatientRecords';
import { useAuth } from '@/lib/auth';

export default function DashboardPage() {
  return (
    <AppShell>
      <Dashboard />
    </AppShell>
  );
}

// Рендерится только после входа (AppShell → RequireAuth), поэтому user не null
function Dashboard() {
  const { user, setUser, hasRole } = useAuth();
  const [isEditing, setIsEditing] = useState(false);
  const [isChangingPassword, setIsChangingPassword] = useState(false);

  if (!user) {
    return null;
  }

  return (
    <>
      <div className="mb-6">
        <h1 className="text-2xl sm:text-3xl font-semibold">Hello, {user.fullName.split(' ')[0]}</h1>
        <p className="mt-1 text-sm text-[#6b756e]">Your documents, test results and how they compare to reference ranges.</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[320px_1fr] gap-6 items-start">
        <ProfileCard user={user} onEdit={() => setIsEditing(true)} onChangePassword={() => setIsChangingPassword(true)} />
        <PatientRecords userId={user.id} />
      </div>

      <UserFormModal
        isOpen={isEditing}
        user={user}
        isAdmin={hasRole('Admin')}
        onClose={() => setIsEditing(false)}
        onSaved={setUser}
      />

      <ChangePasswordModal isOpen={isChangingPassword} onClose={() => setIsChangingPassword(false)} />
    </>
  );
}
