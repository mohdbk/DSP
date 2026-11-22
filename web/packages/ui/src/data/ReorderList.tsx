import type { ReactNode } from 'react';
import { Button, GridList, GridListItem, useDragAndDrop } from 'react-aria-components';
import { t } from '@dsp/core';

// The design system has no reorderable list. React Aria provides drag and drop with full keyboard and
// screen-reader support (pick up with the handle, move with arrow keys, drop with Enter).

export interface ReorderItem { id: string; textValue: string; content: ReactNode }

/** A selectable list whose items can be dragged into a new order. */
export function ReorderList({ label, items, selectedId, onSelect, onMove }: {
  label: string;
  items: ReorderItem[];
  selectedId?: string;
  onSelect: (id: string) => void;
  /** Move `fromId` to the position before or after `toId`. */
  onMove: (fromId: string, toId: string, position: 'before' | 'after') => void;
}) {
  const { dragAndDropHooks } = useDragAndDrop({
    getItems: keys => [...keys].map(key => ({ 'text/plain': String(key) })),
    onReorder: e => {
      const from = [...e.keys][0];
      if (from === undefined) return;
      onMove(String(from), String(e.target.key), e.target.dropPosition === 'before' ? 'before' : 'after');
    },
  });

  return (
    <GridList aria-label={label} items={items} selectionMode="single" selectionBehavior="replace" disallowEmptySelection
      selectedKeys={selectedId ? [selectedId] : []}
      onSelectionChange={keys => { if (keys === 'all') return; const id = [...keys][0]; if (id !== undefined) onSelect(String(id)); }}
      dragAndDropHooks={dragAndDropHooks}
      className="flex flex-col gap-2 outline-none">
      {item => (
        <GridListItem id={item.id} textValue={item.textValue}
          className="flex cursor-pointer items-center gap-3 rounded-lg border border-aeblack-100 bg-whitely-50 px-3 py-2 text-sm text-aeblack-800 outline-none data-[selected]:border-primary-500 data-[selected]:bg-primary-50 data-[hovered]:bg-primary-50 data-[focus-visible]:outline-2 data-[focus-visible]:outline-primary-500 data-[dragging]:opacity-50 data-[drop-target]:border-primary-600">
          <Button slot="drag" aria-label={t('builder.drag')}
            className="cursor-grab rounded px-1 text-aeblack-400 outline-none data-[focus-visible]:outline-2 data-[focus-visible]:outline-primary-500">
            ⠿
          </Button>
          <span className="min-w-0 flex-1">{item.content}</span>
        </GridListItem>
      )}
    </GridList>
  );
}

/** Moves an item within an array (used with ReorderList). */
export function moveItem<T>(list: T[], from: number, to: number, position: 'before' | 'after'): T[] {
  const next = [...list];
  const [item] = next.splice(from, 1);
  let target = to > from ? to - 1 : to;
  if (position === 'after') target += 1;
  next.splice(target, 0, item);
  return next;
}
