'use client';

// Лаборатории-партнёры (только Admin): список, добавление, редактирование, удаление.
// Описание, часы работы и телефон видят пациенты, выбирающие лабораторию в своём городе (/care);
// «Accepting new patients» выключено — лаборатория не показывается в предложениях (текущие связи остаются).
// Сотрудника лаборатории админ создаёт на странице Users (роль «Lab staff» + выбор лаборатории).

import { useState, type FormEvent } from 'react';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import AppShell from '@/components/layout/AppShell';
import Card, { EmptyState, LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import Badge from '@/components/ui/Badge';
import Modal from '@/components/ui/Modal';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import { SelectField, TextField, fieldError } from '@/components/ui/Field';
import { TextAreaField } from '@/components/care/shared';
import { ApiError, getErrorMessage, laboratoriesApi } from '@/lib/api';
import { useApiData } from '@/lib/useApiData';
import { TESTING_CITIES, type Laboratory, type LaboratoryRequest } from '@/types/api';

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
  // null — окно закрыто; 'new' — новая лаборатория; иначе — редактирование
  const [editing, setEditing] = useState<Laboratory | 'new' | null>(null);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-semibold">Laboratories</h1>
          <p className="mt-1 max-w-2xl text-sm text-[#6b756e]">
            Partner laboratories. Patients see the description, hours and phone when choosing a laboratory in their city.
            Create their staff on the Users page with the role “Lab staff”.
          </p>
        </div>
        <Button onClick={() => setEditing('new')}>
          <Plus className="w-4 h-4" />
          Add laboratory
        </Button>
      </div>

      <Card title="Partner laboratories">
        {error && <Alert>{error}</Alert>}
        {!laboratories && !error && <LoadingState />}
        {laboratories && laboratories.length === 0 && <EmptyState>No laboratories yet.</EmptyState>}
        {laboratories && laboratories.length > 0 && (
          <ul className="divide-y divide-[#e1e7dc]">
            {laboratories.map((laboratory) => (
              <li key={laboratory.id} className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{laboratory.name}</span>
                    <Badge>{laboratory.city}</Badge>
                    {!laboratory.acceptingPatients && <Badge tone="amber">Not accepting patients</Badge>}
                  </div>
                  <div className="mt-0.5 text-xs text-[#6b756e]">
                    {[laboratory.address, laboratory.openingHours, laboratory.phone].filter(Boolean).join(' · ') || 'No address or hours yet'}
                  </div>
                  {laboratory.description && <p className="mt-1.5 max-w-2xl text-sm text-[#4a524c]">{laboratory.description}</p>}
                  <div className="mt-1.5 text-xs text-[#8e9890]">
                    {laboratory.staffCount} staff · {laboratory.patientCount} connected patients
                  </div>
                </div>
                <div className="flex shrink-0 gap-1">
                  <Button variant="ghost" size="sm" aria-label={`Edit ${laboratory.name}`} onClick={() => setEditing(laboratory)}>
                    <Pencil className="w-4 h-4" />
                  </Button>
                  <Button variant="ghost" size="sm" aria-label={`Delete ${laboratory.name}`} onClick={() => setToDelete(laboratory)}>
                    <Trash2 className="w-4 h-4" />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Card>

      {editing && (
        <LaboratoryFormModal
          laboratory={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            reload();
          }}
        />
      )}

      <ConfirmDialog
        isOpen={toDelete !== null}
        title="Delete laboratory"
        message={`Delete "${toDelete?.name}"? Its documents stay with the patients; its ${toDelete?.staffCount ?? 0} staff member(s) lose access until assigned to another laboratory, and its patient connections are removed.`}
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

function LaboratoryFormModal({ laboratory, onClose, onSaved }: { laboratory: Laboratory | null; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({
    name: laboratory?.name ?? '',
    city: laboratory?.city ?? (TESTING_CITIES[0] as string),
    address: laboratory?.address ?? '',
    description: laboratory?.description ?? '',
    openingHours: laboratory?.openingHours ?? '',
    phone: laboratory?.phone ?? '',
    acceptingPatients: laboratory?.acceptingPatients ?? true,
  });
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [isSaving, setIsSaving] = useState(false);

  const text = (field: 'name' | 'address' | 'description' | 'openingHours' | 'phone' | 'city') =>
    (event: { target: { value: string } }) => setForm((prev) => ({ ...prev, [field]: event.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setFieldErrors({});
    setIsSaving(true);
    const request: LaboratoryRequest = {
      name: form.name,
      city: form.city,
      address: form.address || null,
      description: form.description || null,
      openingHours: form.openingHours || null,
      phone: form.phone.trim() || null,
      acceptingPatients: form.acceptingPatients,
    };
    try {
      if (laboratory) {
        await laboratoriesApi.update(laboratory.id, request);
      } else {
        await laboratoriesApi.create(request);
      }
      onSaved();
    } catch (err) {
      setError(getErrorMessage(err));
      setFieldErrors(err instanceof ApiError ? err.fieldErrors : {});
      setIsSaving(false);
    }
  };

  return (
    <Modal
      isOpen
      title={laboratory ? `Edit ${laboratory.name}` : 'Add a laboratory'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button type="submit" form="laboratory-form" isLoading={isSaving}>{laboratory ? 'Save changes' : 'Add laboratory'}</Button>
        </>
      }
    >
      <form id="laboratory-form" onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-[2fr_1fr]">
          <TextField label="Name" id="lab-name" required value={form.name} onChange={text('name')} error={fieldError(fieldErrors, 'name')} />
          <SelectField
            label="City"
            id="lab-city"
            value={form.city}
            onChange={text('city')}
            options={TESTING_CITIES.map((city) => ({ value: city, label: city }))}
            error={fieldError(fieldErrors, 'city')}
          />
        </div>
        <TextField label="Address" id="lab-address" value={form.address} onChange={text('address')} error={fieldError(fieldErrors, 'address')} />
        <TextAreaField
          label="Description for patients"
          id="lab-description"
          maxLength={600}
          placeholder="What you test, how fast results are ready, whether an appointment is needed…"
          value={form.description}
          onChange={text('description')}
        />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <TextField label="Opening hours" id="lab-hours" placeholder="Mon–Sat 07:30–18:00" value={form.openingHours} onChange={text('openingHours')} error={fieldError(fieldErrors, 'openingHours')} />
          <TextField label="Phone" id="lab-phone" type="tel" placeholder="+90 2XX XXX XX XX" value={form.phone} onChange={text('phone')} error={fieldError(fieldErrors, 'phone')} />
        </div>
        <label className="flex items-center gap-2.5 text-sm text-[#2f3530]">
          <input
            type="checkbox"
            checked={form.acceptingPatients}
            onChange={(event) => setForm((prev) => ({ ...prev, acceptingPatients: event.target.checked }))}
            className="h-4 w-4 accent-[#737f69]"
          />
          Accepting new patients (shown in patients’ suggestions)
        </label>
        {error && <Alert>{error}</Alert>}
      </form>
    </Modal>
  );
}
