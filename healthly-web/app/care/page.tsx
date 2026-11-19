'use client';

// «Команда здоровья»: одна страница, содержимое зависит от роли.
// Patient — выбор врача и лаборатории, подтверждение доступа (PatientCareTeam);
// Doctor — запросы пациентов и «мои пациенты»; Admin — все связи (ProviderRequests).
// Сотрудник лаборатории работает с запросами на своём рабочем месте /lab.

import AppShell from '@/components/layout/AppShell';
import PatientCareTeam from '@/components/care/PatientCareTeam';
import ProviderRequests from '@/components/care/ProviderRequests';
import { useAuth } from '@/lib/auth';

export default function CarePage() {
  return (
    <AppShell roles={['Patient', 'Doctor', 'Admin']}>
      <CareContent />
    </AppShell>
  );
}

function CareContent() {
  const { user } = useAuth();
  if (!user) {
    return null;
  }
  if (user.role === 'Patient') {
    return <PatientCareTeam />;
  }
  return <ProviderRequests mode={user.role === 'Doctor' ? 'doctor' : 'admin'} />;
}
