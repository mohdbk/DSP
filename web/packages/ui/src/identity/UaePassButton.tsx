import { getLang, t } from '@dsp/core';
import enActive from '../assets/uaepass/UAEPASS_Sign_in_Btn_Default_Black_Active.svg';
import enFocus from '../assets/uaepass/UAEPASS_Sign_in_Btn_Default_Black_Focus.svg';
import enPressed from '../assets/uaepass/UAEPASS_Sign_in_Btn_Default_Black_Pressed.svg';
import enDisabled from '../assets/uaepass/UAEPASS_Sign_in_Btn_Default_Black_Disabled.svg';
import arActive from '../assets/uaepass/AR_UAEPASS_Sign_in_Btn_Default_Black_Active.svg';
import arFocus from '../assets/uaepass/AR_UAEPASS_Sign_in_Btn_Default_Black_Focus.svg';
import arPressed from '../assets/uaepass/AR_UAEPASS_Sign_in_Btn_Default_Black_Pressed.svg';
import arDisabled from '../assets/uaepass/AR_UAEPASS_Sign_in_Btn_Default_Black_Disabled.svg';

// The official UAE PASS "Sign in with UAE PASS" button (UAE PASS Buttons Guidelines, version 2), used unmodified as
// the guidelines and the UAE Design System require. Black style with the default corner radius, for light
// backgrounds. UAE PASS publishes English and Arabic artwork; every other language uses the English artwork.
// Each state (active, focus, pressed, disabled) is its own official artwork, shown by CSS state.

const art = {
  en: { active: enActive, focus: enFocus, pressed: enPressed, disabled: enDisabled },
  ar: { active: arActive, focus: arFocus, pressed: arPressed, disabled: arDisabled },
};

/** Native size of the artwork (it is scaled down on narrow screens, keeping its aspect ratio). */
const WIDTH = 310;
const HEIGHT = 54;

export function UaePassButton({ href, disabled }: { href: string; disabled?: boolean }) {
  const images = getLang() === 'ar' ? art.ar : art.en;
  const label = t('uaePass.signIn');
  const img = (src: string, className: string) => (
    <img src={src} alt="" width={WIDTH} height={HEIGHT} draggable={false} className={`h-auto max-w-full ${className}`} />
  );

  if (disabled) {
    return (
      <span role="link" aria-disabled="true" aria-label={label} className="inline-block cursor-not-allowed">
        {img(images.disabled, 'block')}
      </span>
    );
  }

  return (
    <a href={href} aria-label={label}
      className="group inline-block rounded-xl outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-600">
      {img(images.active, 'block group-focus-visible:hidden group-active:hidden')}
      {img(images.focus, 'hidden group-focus-visible:block group-active:hidden')}
      {img(images.pressed, 'hidden group-active:block')}
    </a>
  );
}
