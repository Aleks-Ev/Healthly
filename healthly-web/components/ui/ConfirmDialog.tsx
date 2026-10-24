'use client';

import { useState } from 'react';
import Modal from './Modal';
import Button from './Button';
import Alert from './Alert';
import { getErrorMessage } from '@/lib/api';

interface ConfirmDialogProps {
  isOpen: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  onClose: () => void;
  // Действие (например, удаление). Ошибка показывается внутри окна
  onConfirm: () => Promise<void>;
}

export default function ConfirmDialog({ isOpen, title, message, confirmLabel = 'Delete', onClose, onConfirm }: ConfirmDialogProps) {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const close = () => {
    setError(null);
    onClose();
  };

  const confirm = async () => {
    setIsLoading(true);
    setError(null);
    try {
      await onConfirm();
      onClose();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      title={title}
      onClose={close}
      footer={
        <>
          <Button variant="secondary" onClick={close}>Cancel</Button>
          <Button variant="danger" onClick={confirm} isLoading={isLoading}>{confirmLabel}</Button>
        </>
      }
    >
      <p className="text-sm text-[#6b756e]">{message}</p>
      {error && <Alert className="mt-4">{error}</Alert>}
    </Modal>
  );
}
