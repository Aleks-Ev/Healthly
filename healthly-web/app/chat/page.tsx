'use client';

// ИИ-чат «Healthly AI» — для любого вошедшего пользователя (у каждого своя история)

import AppShell from '@/components/layout/AppShell';
import HealthChat from '@/components/chat/HealthChat';

export default function ChatPage() {
  return (
    <AppShell>
      <HealthChat />
    </AppShell>
  );
}
