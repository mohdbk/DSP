import { t } from '@dsp/core';
import logo from '../assets/uaepass/uaepass-logo.svg';
import logoDisabled from '../assets/uaepass/uaepass-logo-disabled.svg';

// "Sign in with UAE PASS", built as the UAE PASS Buttons Guidelines (version 2) describe under "Text format": the
// title centred in the button and the official UAE PASS logo beside it, vertically aligned. Black style, white
// title, official logo colours. Sizes, gap, corner radius and the focus, pressed and disabled states are measured
// from the official artwork (see .uaepass-button in styles.css and assets/uaepass/README.md). The title is the
// official wording in the page language, drawn in the portal's font so the spacing holds in every language.

export function UaePassButton({ href, disabled }: { href: string; disabled?: boolean }) {
  const title = t('uaePass.signIn');
  if (disabled) {
    return (
      <span role="link" aria-disabled="true" className="uaepass-button">
        <img src={logoDisabled} alt="" className="uaepass-button__logo" />
        <span>{title}</span>
      </span>
    );
  }
  return (
    <a href={href} className="uaepass-button">
      <img src={logo} alt="" className="uaepass-button__logo" />
      <span>{title}</span>
    </a>
  );
}
