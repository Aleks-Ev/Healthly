'use client';

// Управление пользователями: Doctor — просмотр, Admin — создание, редактирование и удаление.

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { FolderOpen, Pencil, Search, Trash2, UserPlus } from 'lucide-react';
import AppShell from '@/components/layout/AppShell';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import { RoleBadge } from '@/components/ui/Badge';
import UserFormModal from '@/components/users/UserFormModal';
import { usersApi } from '@/lib/api';
import { useApiData } from '@/lib/useApiData';
import { useAuth } from '@/lib/auth';
import { formatDate } from '@/lib/format';
import { ROLES, type Role, type User } from '@/types/api';

export default function UsersPage() {
  return (
    <AppShell roles={['Doctor', 'Admin']}>
      <UsersManager />
    </AppShell>
  );
}

// Стабильная ссылка на функцию загрузки (вне компонента — не пересоздаётся при рендере)
const listUsers = () => usersApi.list();

function UsersManager() {
  const { user: currentUser, hasRole } = useAuth();
  const isAdmin = hasRole('Admin');

  const { data: users, error: loadError, reload } = useApiData(listUsers);
  const [roleFilter, setRoleFilter] = useState<Role | 'All'>('All');
  const [search, setSearch] = useState('');
  // undefined — окно закрыто, null — создание, User — редактирование
  const [editingUser, setEditingUser] = useState<User | null | undefined>(undefined);
  const [userToDelete, setUserToDelete] = useState<User | null>(null);

  // Фильтрация на клиенте: пользователей немного, лишние запросы не нужны
  const visibleUsers = useMemo(() => {
    const query = search.trim().toLowerCase();
    return (users ?? []).filter(
      (user) =>
        (roleFilter === 'All' || user.role === roleFilter) &&
        (query === '' || user.fullName.toLowerCase().includes(query) || user.email.includes(query)),
    );
  }, [users, roleFilter, search]);

  return (
    <>
      <div className="flex flex-wrap items-end justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl sm:text-3xl font-semibold">Users</h1>
          <p className="mt-1 text-sm text-[#6b756e]">
            {isAdmin ? 'Create, edit and remove accounts. Open a patient to see their records.' : 'Open a patient to see their records.'}
          </p>
        </div>
        {isAdmin && (
          <Button onClick={() => setEditingUser(null)}>
            <UserPlus className="w-4 h-4" />
            Create user
          </Button>
        )}
      </div>

      <Card>
        <div className="flex flex-wrap items-center gap-3 mb-4">
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#8e9890]" />
            <input
              aria-label="Search users"
              placeholder="Search by name or email"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              className="w-full rounded-full border border-[#e1e7dc] bg-white pl-9 pr-4 py-2 text-sm outline-none focus:border-[#72826c]"
            />
          </div>
          <div className="flex flex-wrap gap-1" role="tablist" aria-label="Filter by role">
            {(['All', ...ROLES] as const).map((role) => (
              <button
                key={role}
                role="tab"
                aria-selected={roleFilter === role}
                onClick={() => setRoleFilter(role)}
                className={`rounded-full px-3 py-1.5 text-xs font-medium transition ${roleFilter === role ? 'bg-[#72826c] text-white' : 'bg-[#ebefe6] text-[#6b756e] hover:text-[#242b26]'}`}
              >
                {role}
              </button>
            ))}
          </div>
        </div>

        {loadError && <Alert>{loadError}</Alert>}
        {!loadError && users === null && <LoadingState />}
        {users !== null && visibleUsers.length === 0 && <EmptyState>No users match your filters.</EmptyState>}

        {visibleUsers.length > 0 && (
          <div className="overflow-x-auto -mx-5 sm:-mx-6">
            <table className="w-full min-w-[640px] text-sm">
              <thead>
                <tr className="text-left text-xs text-[#6b756e] border-b border-[#e1e7dc]">
                  <th className="font-medium px-5 sm:px-6 py-2">Name</th>
                  <th className="font-medium px-3 py-2">Email</th>
                  <th className="font-medium px-3 py-2">Role</th>
                  <th className="font-medium px-3 py-2">Joined</th>
                  <th className="font-medium px-5 sm:px-6 py-2 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {visibleUsers.map((user) => (
                  <tr key={user.id} className="border-b border-[#e1e7dc] last:border-0">
                    <td className="px-5 sm:px-6 py-3 font-medium">
                      {user.fullName}
                      {user.id === currentUser?.id && <span className="ml-1.5 text-xs text-[#8e9890]">(you)</span>}
                    </td>
                    <td className="px-3 py-3 text-[#6b756e]">{user.email}</td>
                    <td className="px-3 py-3"><RoleBadge role={user.role} /></td>
                    <td className="px-3 py-3 text-[#6b756e]">{formatDate(user.createdAt)}</td>
                    <td className="px-5 sm:px-6 py-3">
                      <div className="flex justify-end gap-1">
                        <Link
                          href={`/admin/users/${user.id}`}
                          aria-label="Open records"
                          className="inline-flex items-center rounded-full px-3 py-1.5 text-[#6b756e] hover:bg-[#ebefe6] hover:text-[#242b26] transition"
                        >
                          <FolderOpen className="w-4 h-4" />
                        </Link>
                        {isAdmin && (
                          <Button variant="ghost" size="sm" aria-label="Edit" onClick={() => setEditingUser(user)}>
                            <Pencil className="w-4 h-4" />
                          </Button>
                        )}
                        {isAdmin && user.id !== currentUser?.id && (
                          <Button variant="ghost" size="sm" aria-label="Delete" onClick={() => setUserToDelete(user)}>
                            <Trash2 className="w-4 h-4" />
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <UserFormModal
        isOpen={editingUser !== undefined}
        user={editingUser ?? null}
        isAdmin={isAdmin}
        onClose={() => setEditingUser(undefined)}
        onSaved={reload}
      />

      <ConfirmDialog
        isOpen={userToDelete !== null}
        title="Delete user"
        message={`Delete ${userToDelete?.fullName} (${userToDelete?.email})? Their documents and test results will be deleted too.`}
        onClose={() => setUserToDelete(null)}
        onConfirm={async () => {
          if (userToDelete) {
            await usersApi.remove(userToDelete.id);
            reload();
          }
        }}
      />
    </>
  );
}
