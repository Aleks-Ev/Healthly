'use client';

// Модальная форма показателя анализа (добавление / редактирование).
// Флаг отклонения: "Auto" — сервер сам сравнит значение с референсным диапазоном.

import { useState, type FormEvent } from 'react';
import Modal from '@/components/ui/Modal';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import { SelectField, TextField, fieldError } from '@/components/ui/Field';
import { ApiError, getErrorMessage, resultsApi } from '@/lib/api';
import type { TestResult } from '@/types/api';

interface TestResultFormModalProps {
  isOpen: boolean;
  documentId: number;
  // null — добавление нового показателя
  result: TestResult | null;
  onClose: () => void;
  onSaved: () => void;
}

const FLAG_OPTIONS = [
  { value: 'auto', label: 'Auto (compare with range)' },
  { value: 'normal', label: 'Normal' },
  { value: 'abnormal', label: 'Out of range' },
];

export default function TestResultFormModal(props: TestResultFormModalProps) {
  if (!props.isOpen) {
    return null;
  }
  return <TestResultForm key={props.result?.id ?? 'new'} {...props} />;
}

function TestResultForm({ documentId, result, onClose, onSaved }: TestResultFormModalProps) {
  const [form, setForm] = useState({
    testName: result?.testName ?? '',
    value: result?.value ?? '',
    unit: result?.unit ?? '',
    referenceRange: result?.referenceRange ?? '',
    // При редактировании показываем сохранённый флаг; "auto" пересчитает его заново
    flag: result ? (result.isAbnormal ? 'abnormal' : 'normal') : 'auto',
  });
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [isSaving, setIsSaving] = useState(false);

  const update = (field: keyof typeof form) => (event: { target: { value: string } }) =>
    setForm((prev) => ({ ...prev, [field]: event.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setFieldErrors({});
    setIsSaving(true);

    const data = {
      testName: form.testName,
      value: form.value,
      unit: form.unit || null,
      referenceRange: form.referenceRange || null,
      isAbnormal: form.flag === 'auto' ? null : form.flag === 'abnormal',
    };

    try {
      if (result) {
        await resultsApi.update(result.id, data);
      } else {
        await resultsApi.add(documentId, data);
      }
      onSaved();
      onClose();
    } catch (err) {
      setError(getErrorMessage(err));
      setFieldErrors(err instanceof ApiError ? err.fieldErrors : {});
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Modal
      isOpen
      title={result ? 'Edit test result' : 'Add test result'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button type="submit" form="result-form" isLoading={isSaving}>{result ? 'Save changes' : 'Add result'}</Button>
        </>
      }
    >
      <form id="result-form" onSubmit={handleSubmit} className="space-y-4">
        <TextField label="Test name" id="result-testName" required placeholder="Hemoglobin" value={form.testName} onChange={update('testName')} error={fieldError(fieldErrors, 'testName')} />
        <div className="grid grid-cols-2 gap-4">
          <TextField label="Value" id="result-value" required placeholder="135" value={form.value} onChange={update('value')} error={fieldError(fieldErrors, 'value')} />
          <TextField label="Unit" id="result-unit" placeholder="g/L" value={form.unit} onChange={update('unit')} error={fieldError(fieldErrors, 'unit')} />
        </div>
        <TextField
          label="Reference range"
          id="result-referenceRange"
          placeholder="120-160"
          hint='Formats: "120-160", "<5", ">=1.2" or text'
          value={form.referenceRange}
          onChange={update('referenceRange')}
          error={fieldError(fieldErrors, 'referenceRange')}
        />
        <SelectField label="Status" id="result-flag" value={form.flag} onChange={update('flag')} options={FLAG_OPTIONS} />
        {error && <Alert>{error}</Alert>}
      </form>
    </Modal>
  );
}
