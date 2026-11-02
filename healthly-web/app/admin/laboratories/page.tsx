'use client';

// Лаборатории-партнёры (только Admin): список, добавление, удаление.
// Сотрудника лаборатории админ создаёт на странице Users (роль «Lab staff» + выбор лаборатории).

import { useState, type FormEvent } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import AppShell from '@/components/layout/AppShell';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import { SelectField, TextField, fieldError } from '@/components/ui/Field';
import { ApiError, getErrorMessage, laboratoriesApi } from '@/lib/api';
import { useApiData } from '@/lib/useApiData';
import { TESTING_CITIES, type Laboratory } from '@/types/api';

export default function LaboratoriesPage() {
  return (
    <AppShell roles={['Admin']}>
      <LaboratoriesManager />
    </AppShell>
  );
}

function LaboratoriesManager() {
  const { data: laboratories, error, reload } = useApiData(laboratoriesApi.list);
  const [toDelete, setToDelete] = useState<Laboratory | null>(null);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl sm:text-3xl font-semibold">Laboratories</h1>
        <p className="mt-1 text-sm text-[#6b756e]">
          Partner laboratories. Create their staff on the Users page with the role “Lab staff”.
        </p>
      </div>

      <AddLaboratoryCard onAdded={reload} />

      <Card title="Partner laboratories">
        {error && <Alert>{error}</Alert>}
        {!laboratories && !error && <LoadingState />}
        {laboratories && laboratories.length === 0 && <EmptyState>No laboratories yet.</EmptyState>}
        {laboratories && laboratories.length > 0 && (
          <div className="overflow-x-auto -mx-5 sm:-mx-6">
            <table className="w-full min-w-[560px] text-sm">
              <thead>
                <tr className="text-left text-xs text-[#6b756e] border-b border-[#e1e7dc]">
                  <th className="font-medium px-5 sm:px-6 py-2">Name</th>
                  <th className="font-medium px-3 py-2">City</th>
                  <th className="font-medium px-3 py-2">Address</th>
                  <th className="font-medium px-3 py-2">Staff</th>
                  <th className="font-medium px-5 sm:px-6 py-2 text-right" />
                </tr>
              </thead>
              <tbody>
                {laboratories.map((laboratory) => (
                  <tr key={laboratory.id} className="border-b border-[#e1e7dc] last:border-0">
                    <td className="px-5 sm:px-6 py-3 font-medium">{laboratory.name}</td>
                    <td className="px-3 py-3">{laboratory.city}</td>
                    <td className="px-3 py-3 text-[#6b756e]">{laboratory.address || '—'}</td>
                    <td className="px-3 py-3">{laboratory.staffCount}</td>
                    <td className="px-5 sm:px-6 py-3 text-right">
                      <Button variant="ghost" size="sm" aria-label="Delete" onClick={() => setToDelete(laboratory)}>
                        <Trash2 className="w-4 h-4" />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <ConfirmDialog
        isOpen={toDelete !== null}
        title="Delete laboratory"
        message={`Delete "${toDelete?.name}"? Its documents stay with the patients; its ${toDelete?.staffCount ?? 0} staff member(s) lose access until assigned to another laboratory.`}
        onClose={() => setToDelete(null)}
        onConfirm={async () => {
          if (toDelete) {
            await laboratoriesApi.remove(toDelete.id);
            reload();
          }
        }}
      />
    </div>
  );
}

function AddLaboratoryCard({ onAdded }: { onAdded: () => void }) {
  const [form, setForm] = useState({ name: '', city: TESTING_CITIES[0] as string, address: '' });
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [isSaving, setIsSaving] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setFieldErrors({});
    setIsSaving(true);
    try {
      await laboratoriesApi.create({ name: form.name, city: form.city, address: form.address || null });
      setForm((prev) => ({ ...prev, name: '', address: '' }));
      onAdded();
    } catch (err) {
      setError(getErrorMessage(err));
      setFieldErrors(err instanceof ApiError ? err.fieldErrors : {});
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Card title="Add a laboratory">
      <form onSubmit={handleSubmit} className="grid grid-cols-1 sm:grid-cols-[2fr_1fr_2fr_auto] gap-3 items-end">
        <TextField label="Name" id="lab-name" required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} error={fieldError(fieldErrors, 'name')} />
        <SelectField
          label="City"
          id="lab-city"
          value={form.city}
          onChange={(e) => setForm({ ...form, city: e.target.value })}
          options={TESTING_CITIES.map((city) => ({ value: city, label: city }))}
          error={fieldError(fieldErrors, 'city')}
        />
        <TextField label="Address" id="lab-address" value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} error={fieldError(fieldErrors, 'address')} />
        <Button type="submit" isLoading={isSaving}>
          <Plus className="w-4 h-4" />
          Add
        </Button>
      </form>
      {error && <Alert className="mt-4">{error}</Alert>}
    </Card>
  );
}
