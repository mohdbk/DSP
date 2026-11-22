import type { ReactNode } from 'react';
import { AegovButton, AegovModal } from '@aegov/designsystem-react';
import { t } from '@dsp/core';
import { Icon, X } from '../icons';

/**
 * A dialog (AEGOV modal) for creating, editing and confirming: a title, the content, and the actions at the bottom.
 * The modal is always mounted with one stable child, because AEGOV may move its content; React only changes what
 * is inside. Escape, the close button and (unless busy) the backdrop call onClose.
 */
export function Modal({ open, title, onClose, children, actions, size = 'base', busy = false }: {
  open: boolean;
  title: string;
  onClose: () => void;
  children?: ReactNode;
  /** Buttons at the bottom, end-aligned (cancel first, then the main action). */
  actions?: ReactNode;
  size?: 'sm' | 'base' | 'lg' | 'xl';
  /** While saving: the backdrop doesn't close it. */
  busy?: boolean;
}) {
  return (
    <AegovModal isOpen={open} label={title} size={size} isCloseWithOverlay={!busy}
      onAegovModalClose={() => { if (open) onClose(); }}>
      <div className="flex max-h-[85vh] flex-col">
        <div className="mb-4 flex items-start justify-between gap-4">
          <h2 className="text-h5 font-bold text-aeblack-900">{title}</h2>
          <button type="button" aria-label={t('close')} title={t('close')} disabled={busy} onClick={onClose}
            className="flex size-10 shrink-0 items-center justify-center rounded-lg text-aeblack-700 hover:bg-aeblack-50 focus-visible:outline-2 focus-visible:outline-primary-600 disabled:opacity-40">
            <Icon icon={X} />
          </button>
        </div>
        <div className="-mx-1 flex-1 overflow-y-auto px-1">{open && children}</div>
        {actions && <div className="mt-6 flex flex-wrap items-center justify-end gap-3 border-t border-aeblack-100 pt-4">{actions}</div>}
      </div>
    </AegovModal>
  );
}

/** Asks before something that can't be undone (delete, discard). The confirm button is red for destructive actions. */
export function ConfirmDialog({ open, title, message, confirmLabel, onConfirm, onCancel, busy = false, destructive = true }: {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
  busy?: boolean;
  destructive?: boolean;
}) {
  return (
    <Modal open={open} title={title} onClose={onCancel} size="sm" busy={busy}
      actions={(
        <>
          <AegovButton appearance="outline" variant="neutral-max" isDisabled={busy} onAegovButtonClick={onCancel}>{t('cancel')}</AegovButton>
          <AegovButton variant={destructive ? 'danger' : 'primary'} isLoading={busy} onAegovButtonClick={onConfirm}>{confirmLabel}</AegovButton>
        </>
      )}>
      <p className="text-aeblack-700">{message}</p>
    </Modal>
  );
}
