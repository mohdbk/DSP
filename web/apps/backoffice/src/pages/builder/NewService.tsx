import { useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { ApiError, t, type BuilderService, type FieldError, type ServiceDefinition } from '@dsp/core';
import { AegovButton, AegovLink, Notice, PageHeader, Panel, TextField } from '@dsp/ui';
import { api } from '../../session';

/** A starting point for a new service: one step, one field, ready to edit. */
function blankService(code: string, name: string, nameAr: string): ServiceDefinition {
  return {
    code, name, nameAr, description: '', descriptionAr: '', category: 'General', categoryAr: 'عام',
    fee: 100, currency: 'AED', referenceField: null, workflowVersion: 1, reviewChecklist: [],
    steps: [{
      id: 'applicant', title: 'Applicant details', titleAr: 'بيانات مقدم الطلب', fields: [
        { key: 'fullName', label: 'Full name', labelAr: 'الاسم الكامل', type: 'text', required: true, maxLength: 150, prefillFrom: 'name' },
      ],
    }],
  };
}

/** New service: choose its code and name, then edit it as a draft. */
export function NewServicePage() {
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [error, setError] = useState<string>();
  const err = (field: string) => errors.find(e => e.field === field)?.message;

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    setErrors([]);
    try {
      const created = await api.post<BuilderService>('/api/v1/eservices/builder/services', blankService(code.trim(), name.trim(), nameAr.trim()));
      navigate({ to: '/services/$code', params: { code: created.code }, replace: true });
    } catch (ex) {
      if (ex instanceof ApiError && ex.errors.length) setErrors(ex.errors);
      else setError((ex as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title={t('builder.createTitle')} actions={<AegovLink href="/services">{t('builder.backToList')}</AegovLink>} />
      <Panel>
        <form onSubmit={create}>
          {error && <Notice kind="error">{error}</Notice>}
          <TextField id="code" label={t('builder.code')} hint={t('builder.codeHelp')} error={err('code')} dir="ltr" required
            placeholder="BUSINESS_PERMIT" value={code} onChange={v => setCode(v.toUpperCase().replace(/[^A-Z0-9_]/g, '_'))} />
          <div className="grid gap-x-4 sm:grid-cols-2">
            <TextField id="name" label={t('builder.nameEn')} dir="ltr" required value={name} onChange={setName} error={err('name')} />
            <TextField id="nameAr" label={t('builder.nameAr')} dir="rtl" value={nameAr} onChange={setNameAr} error={err('nameAr')} />
          </div>
          <AegovButton type="submit" isLoading={busy} isDisabled={!code || !name}>{t('builder.create')}</AegovButton>
        </form>
      </Panel>
    </div>
  );
}
