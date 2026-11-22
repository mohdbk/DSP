import type { ReactNode } from 'react';

/** Label–value pairs (application summaries, read-only answers). */
export function DescriptionList({ items }: { items: { label: ReactNode; value: ReactNode }[] }) {
  return (
    <dl className="m-0 divide-y divide-aeblack-100">
      {items.map((item, i) => (
        <div key={i} className="flex flex-wrap justify-between gap-x-6 gap-y-1 py-3">
          <dt className="text-sm text-aeblack-600">{item.label}</dt>
          <dd className="m-0 text-end font-medium text-aeblack-900">{item.value}</dd>
        </div>
      ))}
    </dl>
  );
}
