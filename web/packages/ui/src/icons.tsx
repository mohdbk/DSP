import type { ReactNode } from 'react';
import type { Icon as PhosphorIcon, IconWeight } from '@phosphor-icons/react';

// Icons follow the AEGOV iconography rules: Phosphor only, inline SVG in currentColor, 24px (size-6) next to body
// text, hidden from screen readers when a visible label is next to them, mirrored in RTL only when directional
// (arrows, chevrons, back/next), and used with a text label on navigation, header links, actions, service cards,
// help, status and empty states — not on every list item.

export {
  ArrowLeft, ArrowRight, Bell, Certificate, ChatCircleText, Coins, CreditCard, FileText, Files, FloppyDisk, FolderOpen, GearSix,
  Globe, House, ListChecks, MagnifyingGlass, PaperPlaneRight, Plus, Robot, SignOut, Translate, Tray, Wrench,
} from '@phosphor-icons/react';

/** One icon at an AEGOV size, decorative by default. */
export function Icon({ icon: Glyph, size = 'size-6', weight = 'regular', mirror = false, className = '' }: {
  icon: PhosphorIcon;
  /** size-5 next to small text, size-6 by default, size-8 card headers, size-10 service identity, size-12 states. */
  size?: 'size-5' | 'size-6' | 'size-7' | 'size-8' | 'size-10' | 'size-12';
  weight?: IconWeight;
  /** Directional icons (arrows, back/next) turn around in right-to-left pages. */
  mirror?: boolean;
  className?: string;
}) {
  return <Glyph aria-hidden="true" weight={weight} className={`${size} shrink-0 ${mirror ? 'rtl:-scale-x-100' : ''} ${className}`} />;
}

/** An icon before a label, centred on its first line with the standard gap. Use inside buttons and links. */
export function IconLabel({ icon, mirror, children }: { icon: PhosphorIcon; mirror?: boolean; children: ReactNode }) {
  return (
    <span className="inline-flex items-center gap-2">
      <Icon icon={icon} mirror={mirror} />
      <span>{children}</span>
    </span>
  );
}
