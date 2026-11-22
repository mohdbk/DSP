import { useMemo, useState } from 'react';
import { getRouteApi, useNavigate } from '@tanstack/react-router';
import { ApiError, formatDate, formatMoney, initCurrencies, loc, t, type FieldError, type LocalizedText } from '@dsp/core';
import {
  AegovButton, ConfirmDialog, Modal, ChoiceField, type ColumnDef, DataTable, FloppyDisk, IconLabel, listStateProps, Money, MultilingualField, Notice, NumberField, PageHeader, Plus, Tag, TextField,
} from '@dsp/ui';
import { api } from '../../session';

const route = getRouteApi('/settings/currencies');
const base = '/api/v1/platform/admin/currencies';

interface CurrencyAdmin {
  code: string; name: LocalizedText; symbol: string; symbolPosition: 'before' | 'after'; decimals: number;
  isEnabled: boolean; isDefault: boolean; sortOrder: number; updatedAt: string; updatedBy: string;
}

interface Draft { code: string; name: LocalizedText | null; symbol: string; symbolPosition: 'before' | 'after'; decimals: number; sortOrder: number }

/** How an amount reads with the draft's settings, for the live example. */
function example(d: Draft): string {
  const value = (1500.5).toLocaleString('en-AE', { minimumFractionDigits: d.decimals, maximumFractionDigits: d.decimals });
  const symbol = d.symbol || d.code || '…';
  return d.symbolPosition === 'after' ? `${value} ${symbol}` : `${symbol} ${value}`;
}

/** Currencies services can charge in: add, rename, set symbol and decimals, enable, disable, make default, delete. */
export function CurrenciesPage() {
  const navigate = useNavigate();
  const search = route.useSearch();
  // The rows on the page shown (the table loads them from the server) and how many currencies there are.
  const [currencies, setCurrencies] = useState<CurrencyAdmin[]>([]);
  const [count, setCount] = useState(0);
  const [reloadKey, setReloadKey] = useState(0);
  const [selected, setSelected] = useState<string | 'new'>();
  const [draft, setDraft] = useState<Draft>();
  const [errors, setErrors] = useState<FieldError[]>([]);
  const [message, setMessage] = useState<{ kind: 'success' | 'error'; text: string }>();
  const [busy, setBusy] = useState<string>();
  const [confirmDelete, setConfirmDelete] = useState(false);

  const load = () => setReloadKey(n => n + 1);

  const current = currencies.find(c => c.code === selected);
  const select = (code: string | 'new') => {
    setSelected(code);
    setErrors([]);
    setMessage(undefined);
    setConfirmDelete(false);
    const c = currencies.find(x => x.code === code);
    setDraft(c
      ? { code: c.code, name: c.name, symbol: c.symbol, symbolPosition: c.symbolPosition, decimals: c.decimals, sortOrder: c.sortOrder }
      : { code: '', name: null, symbol: '', symbolPosition: 'before', decimals: 2, sortOrder: count * 10 });
  };

  async function run(kind: string, action: () => Promise<unknown>, success: string) {
    setBusy(kind);
    setMessage(undefined);
    try {
      await action();
      load();
      await initCurrencies(); // this portal formats amounts with the new settings at once
      setMessage({ kind: 'success', text: success });
      setErrors([]);
      if (kind === 'save' || kind === 'delete') close();
    } catch (e) {
      if (e instanceof ApiError && e.errors.length) setErrors(e.errors);
      setMessage({ kind: 'error', text: (e as Error).message });
    } finally {
      setBusy(undefined);
    }
  }

  const err = (field: string) => errors.find(e => e.field === field)?.message;
  const close = () => { setDraft(undefined); setSelected(undefined); setConfirmDelete(false); setErrors([]); };
  const body = () => ({
    code: draft!.code.trim().toUpperCase(), name: draft!.name ?? {}, symbol: draft!.symbol.trim() || draft!.code.trim().toUpperCase(),
    symbolPosition: draft!.symbolPosition, decimals: draft!.decimals, sortOrder: draft!.sortOrder,
  });

  const columns = useMemo<ColumnDef<CurrencyAdmin, string>[]>(() => [
    {
      id: 'currency', header: t('settings.currency'), accessorFn: c => `${c.code} ${loc(c.name)}`,
      cell: c => (
        <span>
          <strong><bdi>{c.row.original.code}</bdi></strong>
          <span className="block text-xs text-aeblack-500">{loc(c.row.original.name, c.row.original.code)}</span>
        </span>
      ),
    },
    {
      id: 'status', header: t('status'),
      accessorFn: c => (c.isDefault ? 'default' : c.isEnabled ? 'enabled' : 'disabled'),
      cell: c => <Tag variant={c.getValue() === 'disabled' ? 'slate' : c.getValue() === 'default' ? 'aegold' : 'aegreen'}>{t(`settings.lang.${c.getValue()}`)}</Tag>,
      meta: { filter: true, filterLabel: v => t(`settings.lang.${v}`) },
    },
    { id: 'decimals', header: t('settings.decimals'), accessorFn: c => String(c.decimals), meta: { type: 'number' } },
    {
      id: 'example', header: t('settings.example'), accessorFn: c => formatMoney(1500.5, c.code), cell: c => <Money>{c.getValue()}</Money>,
      enableSorting: false, meta: { filterable: false },
    },
    { id: 'updated', header: t('updated'), accessorFn: c => c.updatedAt, cell: c => `${formatDate(c.getValue(), true)} · ${c.row.original.updatedBy}`, meta: { type: 'date' } },
  ], []);

  return (
    <>
      <PageHeader title={t('settings.currencies')} intro={t('settings.currenciesIntro')}
        actions={<AegovButton onAegovButtonClick={() => select('new')}><IconLabel icon={Plus}>{t('settings.addCurrency')}</IconLabel></AegovButton>} />
      {message && !draft && <Notice kind={message.kind}>{message.text}</Notice>}

      <Modal open={!!draft && !confirmDelete} size="lg" busy={!!busy} onClose={close}
        title={selected === 'new' ? t('settings.addCurrency') : t('settings.editCurrency', { currency: draft?.code ?? '' })}
        actions={draft && (
          <>
            <span className="me-auto flex flex-wrap gap-3">
          {current && !current.isDefault && (
            <>
              <AegovButton appearance="outline" isDisabled={!!busy} isLoading={busy === 'toggle'}
                onAegovButtonClick={() => run('toggle', () => api.post(`${base}/${current.code}/${current.isEnabled ? 'disable' : 'enable'}`),
                  t(current.isEnabled ? 'settings.disabled' : 'settings.enabled'))}>
                {t(current.isEnabled ? 'settings.disable' : 'settings.enable')}
              </AegovButton>
              <AegovButton appearance="outline" isDisabled={!!busy} isLoading={busy === 'default'}
                onAegovButtonClick={() => run('default', () => api.post(`${base}/${current.code}/default`), t('settings.defaultSet'))}>
                {t('settings.makeDefault')}
              </AegovButton>
              <AegovButton appearance="link" variant="danger" isDisabled={!!busy} onAegovButtonClick={() => setConfirmDelete(true)}>
                {t('settings.delete')}
              </AegovButton>
            </>
          )}
            </span>
            <AegovButton appearance="outline" variant="neutral-max" isDisabled={!!busy} onAegovButtonClick={close}>{t('cancel')}</AegovButton>
          <AegovButton isDisabled={!!busy} isLoading={busy === 'save'}
            onAegovButtonClick={() => run('save', () => (selected === 'new' ? api.post(base, body()) : api.put(`${base}/${draft.code}`, body())),
              t('settings.saved'))}>
            {selected === 'new' ? <IconLabel icon={Plus}>{t('settings.addCurrency')}</IconLabel> : <IconLabel icon={FloppyDisk}>{t('save')}</IconLabel>}
          </AegovButton>
          </>
        )}>
        {draft && (
          <>
            {message && <Notice kind={message.kind}>{message.text}</Notice>}
      <div className="grid gap-x-4 md:grid-cols-3">
        <TextField id="code" label={t('settings.currencyCode')} hint={t('settings.currencyCodeHint')} dir="ltr" required
          disabled={selected !== 'new'} value={draft.code} error={err('code')}
          onChange={v => setDraft({ ...draft, code: v.toUpperCase().replace(/[^A-Z]/g, '').slice(0, 3) })} />
        <TextField id="symbol" label={t('settings.symbol')} hint={t('settings.symbolHint')} dir="ltr" value={draft.symbol}
          error={err('symbol')} onChange={v => setDraft({ ...draft, symbol: v.slice(0, 10) })} />
        <NumberField id="decimals" label={t('settings.decimals')} hint={t('settings.decimalsHint')} value={draft.decimals}
          error={err('decimals')} onChange={v => setDraft({ ...draft, decimals: Math.max(0, Math.min(4, Math.round(v ?? 0))) })} />
      </div>
      <div className="grid gap-x-4 md:grid-cols-3">
        <ChoiceField id="position" label={t('settings.symbolPosition')} value={draft.symbolPosition} error={err('symbolPosition')}
          choices={[{ value: 'before', label: t('settings.position.before') }, { value: 'after', label: t('settings.position.after') }]}
          onChange={v => setDraft({ ...draft, symbolPosition: v as 'before' | 'after' })} />
        <NumberField id="order" label={t('settings.order')} value={draft.sortOrder} onChange={v => setDraft({ ...draft, sortOrder: v ?? 0 })} />
        <div className="mb-5">
          <p className="mb-2 font-semibold text-aeblack-800">{t('settings.example')}</p>
          <p className="text-lg"><Money>{example(draft)}</Money></p>
        </div>
      </div>
      <MultilingualField id="name" label={t('settings.currencyName')} value={draft.name}
        error={c => err(`name.${c}`) ?? err('name')} onChange={v => setDraft({ ...draft, name: v })} />
            {selected === 'new' && <p className="text-sm text-aeblack-600">{t('settings.newCurrencyNote')}</p>}
          </>
        )}
      </Modal>

      <ConfirmDialog open={confirmDelete} title={t('settings.deleteTitle', { name: draft?.code ?? '' })}
        message={t('settings.deleteCurrencyConfirm')} confirmLabel={t('settings.delete')} busy={busy === 'delete'}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={() => { if (current) void run('delete', () => api.del(`${base}/${current.code}`), t('settings.deleted')); }} />

      <div>
        <DataTable label={t('settings.currencies')} source={base} listKey="backoffice.currencies" reloadKey={reloadKey}
          onLoad={p => { setCurrencies(p.items); setCount(p.total); }} columns={columns} getRowId={c => c.code}
          onRowAction={c => select(c.code)}
          {...listStateProps(search, next => navigate({ to: '/settings/currencies', search: next, replace: true }))}
          emptyTitle={t('settings.noCurrencies')} />
      </div>
    </>
  );
}
