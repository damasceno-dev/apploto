'use client';

import Link from 'next/link';
import {
  createContext,
  useContext,
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactNode,
} from 'react';
import { cn } from '@/lib/cn';
import { Dialog, useCompactOutsideDialog } from './dialog';
import { CheckIcon, ChevronDownIcon } from './icons';
import { foldText, Popover } from './popover';

// Menu (design/specs/components/menu.md): a popover list of actions or destinations opened from a
// trigger (Session menu, row overflow). Menu-button pattern: arrows with wrap, Home/End and
// type-ahead move a roving focus; Escape, an outside press, a choice or Tab close it and focus
// returns to the trigger. Unavailable items stay focusable so their reason can be read. On compact
// layouts the same items, in the same order, open in a bottom sheet.

interface MenuApi {
  readonly close: () => void;
  readonly sheet: boolean;
}
const MenuContext = createContext<MenuApi | null>(null);

function useMenu(): MenuApi {
  const api = useContext(MenuContext);
  if (api === null) throw new Error('Menu items must be inside Menu.');
  return api;
}

const itemSelector = '[role="menuitem"], [role="menuitemradio"], [data-menu-item]';

export function Menu({
  trigger,
  triggerLabel,
  header,
  title,
  className,
  children,
}: {
  /** The trigger's visible content (a label, or avatar plus name). */
  readonly trigger: ReactNode;
  /** The trigger's accessible name when its content is not enough (**Conta de Ricardo Moura, Gestão**). */
  readonly triggerLabel?: string;
  /** Identity block above the items (name, e-mail, branch and role) — not an item. */
  readonly header?: ReactNode;
  /** The bottom sheet's title on compact layouts. */
  readonly title: string;
  readonly className?: string;
  readonly children: ReactNode;
}) {
  // Inside a dialog the menu stays a popover: one modal layer only.
  const compact = useCompactOutsideDialog();
  const menuId = useId();
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const typed = useRef({ text: '', at: 0 });

  const items = () => [...(menuRef.current?.querySelectorAll<HTMLElement>(itemSelector) ?? [])];
  const focusItem = (index: number) => {
    const list = items();
    if (list.length === 0) return;
    list[((index % list.length) + list.length) % list.length]?.focus();
  };
  // Which item takes focus once the opened menu has rendered.
  const pendingFocus = useRef<'first' | 'last' | null>(null);
  useEffect(() => {
    if (!open || pendingFocus.current === null) return;
    const list = [...(menuRef.current?.querySelectorAll<HTMLElement>(itemSelector) ?? [])];
    (pendingFocus.current === 'first' ? list[0] : list.at(-1))?.focus();
    pendingFocus.current = null;
  }, [open]);
  const show = (focus: 'first' | 'last') => {
    pendingFocus.current = focus;
    setOpen(true);
  };
  const close = () => {
    setOpen(false);
    triggerRef.current?.focus();
  };

  const onTriggerKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (compact) return;
    if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      show('first');
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      show('last');
    }
  };

  const onMenuKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const list = items();
    const current = list.indexOf(document.activeElement as HTMLElement);
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      focusItem(current + 1);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      focusItem(current - 1);
    } else if (event.key === 'Home') {
      event.preventDefault();
      focusItem(0);
    } else if (event.key === 'End') {
      event.preventDefault();
      focusItem(-1);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      close();
    } else if (event.key === 'Tab') {
      setOpen(false);
    } else if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
      const now = event.timeStamp;
      typed.current = {
        text: now - typed.current.at < 500 ? typed.current.text + event.key : event.key,
        at: now,
      };
      const query = foldText(typed.current.text);
      for (let step = 1; step <= list.length; step += 1) {
        const index = (current + step) % list.length;
        if (
          foldText(list[index]?.textContent ?? '')
            .trim()
            .startsWith(query)
        ) {
          focusItem(index);
          break;
        }
      }
    }
  };

  return (
    <MenuContext.Provider value={{ close, sheet: compact }}>
      <button
        ref={triggerRef}
        type="button"
        aria-haspopup={compact ? 'dialog' : 'menu'}
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-label={triggerLabel}
        onClick={() => (open ? close() : compact ? setOpen(true) : show('first'))}
        onKeyDown={onTriggerKeyDown}
        className={cn(
          'inline-flex min-h-size-touch-target-min items-center gap-inline-sm rounded-control px-2 type-control text-text-primary hover:bg-surface-hover',
          className,
        )}
      >
        {trigger}
        <ChevronDownIcon className="text-text-muted" />
      </button>
      {compact ? (
        <Dialog open={open} onDismiss={close} title={title} variant="sheet" initialFocus="title">
          {header ? (
            <div className="border-b border-border-subtle pb-stack-sm">{header}</div>
          ) : null}
          <div ref={menuRef} id={menuId} className="flex flex-col py-stack-xs">
            {children}
          </div>
        </Dialog>
      ) : (
        <Popover
          open={open}
          anchor={triggerRef}
          onDismiss={() => setOpen(false)}
          className="w-max max-w-menu-max"
        >
          {header ? (
            <div className="border-b border-border-subtle px-inset-panel py-stack-sm">{header}</div>
          ) : null}
          <div
            ref={menuRef}
            id={menuId}
            role="menu"
            aria-label={title}
            onKeyDown={onMenuKeyDown}
            className="flex flex-col py-stack-xs"
          >
            {children}
          </div>
        </Popover>
      )}
    </MenuContext.Provider>
  );
}

const itemClass =
  'flex min-h-size-touch-target-min w-full cursor-pointer items-start gap-inline-sm px-inset-panel py-2 text-start type-control text-text-primary outline-none hover:bg-surface-hover focus-visible:bg-surface-hover focus-visible:outline-2 focus-visible:-outline-offset-2';

function ItemText({
  label,
  description,
}: {
  readonly label: ReactNode;
  readonly description?: ReactNode;
}) {
  return (
    <span className="flex min-w-0 flex-col">
      <span>{label}</span>
      {description ? <span className="type-helper text-text-muted">{description}</span> : null}
    </span>
  );
}

/** A command (**Sair**). `busyLabel` (**Saindo…**) blocks it while its request runs. */
export function MenuItem({
  onSelect,
  label,
  description,
  busy = false,
  busyLabel,
}: {
  readonly onSelect: () => void;
  readonly label: ReactNode;
  readonly description?: ReactNode;
  readonly busy?: boolean;
  readonly busyLabel?: ReactNode;
}) {
  const { close, sheet } = useMenu();
  return (
    <button
      type="button"
      role={sheet ? undefined : 'menuitem'}
      data-menu-item=""
      tabIndex={sheet ? 0 : -1}
      aria-busy={busy || undefined}
      aria-disabled={busy || undefined}
      onClick={() => {
        if (busy) return;
        onSelect();
        close();
      }}
      className={itemClass}
    >
      <ItemText label={busy && busyLabel ? busyLabel : label} description={description} />
    </button>
  );
}

/** A destination (**Minha conta**): a real link. */
export function MenuLink({
  href,
  label,
  description,
}: {
  readonly href: string;
  readonly label: ReactNode;
  readonly description?: ReactNode;
}) {
  const { close, sheet } = useMenu();
  return (
    <Link
      href={href as never}
      role={sheet ? undefined : 'menuitem'}
      data-menu-item=""
      tabIndex={sheet ? 0 : -1}
      onClick={close}
      className={itemClass}
    >
      <ItemText label={label} description={description} />
    </Link>
  );
}

/** A group with an optional overline heading (**Modo de trabalho**). */
export function MenuGroup({
  label,
  children,
}: {
  readonly label?: string;
  readonly children: ReactNode;
}) {
  const { sheet } = useMenu();
  const id = useId();
  return (
    <div
      role={sheet ? 'group' : 'group'}
      aria-labelledby={label ? id : undefined}
      className="border-t border-border-subtle py-stack-xs first:border-t-0"
    >
      {label ? (
        <span id={id} className="block px-inset-panel py-1 type-overline text-text-muted">
          {label}
        </span>
      ) : null}
      {children}
    </div>
  );
}

/** One choice of a mode group (Gestão / Meu turno): choosing it changes mode and closes the menu. */
export function MenuRadioItem({
  checked,
  onSelect,
  label,
  description,
}: {
  readonly checked: boolean;
  readonly onSelect: () => void;
  readonly label: ReactNode;
  readonly description?: ReactNode;
}) {
  const { close, sheet } = useMenu();
  return (
    <button
      type="button"
      role={sheet ? 'radio' : 'menuitemradio'}
      aria-checked={checked}
      data-menu-item=""
      tabIndex={sheet ? 0 : -1}
      onClick={() => {
        onSelect();
        close();
      }}
      className={cn(itemClass, checked && 'bg-surface-selected')}
    >
      <span className="flex h-lh w-size-icon shrink-0 items-center">
        {checked ? <CheckIcon /> : null}
      </span>
      <ItemText label={label} description={description} />
    </button>
  );
}

/**
 * An unavailable item: label in the disabled text color, its reason beneath and, when one exists, a
 * link to fix it. It stays focusable (`aria-disabled`) so the reason is reachable.
 */
export function MenuUnavailable({
  label,
  reason,
  fix,
}: {
  readonly label: ReactNode;
  readonly reason: ReactNode;
  readonly fix?: { readonly label: string; readonly href: string };
}) {
  const { close, sheet } = useMenu();
  const reasonId = useId();
  return (
    <div className="flex flex-col">
      <button
        type="button"
        role={sheet ? undefined : 'menuitem'}
        data-menu-item=""
        tabIndex={sheet ? 0 : -1}
        aria-disabled="true"
        aria-describedby={reasonId}
        onClick={(event) => event.preventDefault()}
        className={cn(itemClass, 'cursor-not-allowed text-text-disabled hover:bg-transparent')}
      >
        <ItemText label={label} />
      </button>
      <span id={reasonId} className="px-inset-panel type-helper text-text-muted">
        {reason}
      </span>
      {fix ? (
        <Link
          href={fix.href as never}
          role={sheet ? undefined : 'menuitem'}
          data-menu-item=""
          tabIndex={sheet ? 0 : -1}
          onClick={close}
          className={cn(itemClass, 'text-text-link')}
        >
          {fix.label}
        </Link>
      ) : null}
    </div>
  );
}

export function MenuSeparator() {
  const { sheet } = useMenu();
  return <hr role={sheet ? undefined : 'separator'} className="my-stack-xs border-border-subtle" />;
}
