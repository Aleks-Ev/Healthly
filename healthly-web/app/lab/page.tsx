'use client';

// Рабочее место сотрудника лаборатории (только LabStaff): поиск пациента по Healthly ID, загрузка бланка, очередь
import AppShell from '@/components/layout/AppShell';
import LabWorkspace from '@/components/lab/LabWorkspace';

export default function LabPage() {
  return (
    <AppShell roles={['LabStaff']}>
      <LabWorkspace />
    </AppShell>
  );
}
