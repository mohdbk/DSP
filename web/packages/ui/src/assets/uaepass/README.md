# UAE PASS button

`UaePassButton` builds "Sign in with UAE PASS" the way the UAE PASS Buttons Guidelines (version 2) describe under
"Text format": the official title centred in the button, the official UAE PASS logo beside it, vertically aligned.
Guidelines: https://docs.uaepass.ae/guidelines/design-guidelines/uaepass-button-guideline

Why it is built rather than an image: the official SVGs set the title as live text in SF Pro Text Semibold (English)
and GE SS Two Medium (Arabic), at fixed positions. Those fonts can't be bundled (SF Pro is licensed by Apple for its
own platforms only; GE SS Two is commercial), so on most devices the title came out in another font and the gap
between logo and title broke (about 30 px in Arabic instead of 4 px). The official PNGs exist only at 1x and blur on
phones.

Files here:

- `uaepass-logo.svg`: the UAE PASS logo taken unchanged from the official black button artwork (white, green, red).
- `uaepass-logo-disabled.svg`: the logo from the official disabled artwork (black at 40%).

Measurements (from the official black, default-radius artwork; kept in `.uaepass-button` in `styles.css`):
300 × 44 px button, 12 px radius, 24 px logo, about 4 px gap, white title 19 px semibold (Arabic 17 px medium),
soft 2 px shadow. Focus: 2 px UAE PASS green (#00AC75) halo at 30%. Pressed: #383838 with a #231F20 border.
Disabled: white with a 40% border, title and logo at 40% black.

Rules from the guidelines:

- Title exactly as published, capitalisation and spacing kept: "Sign in with UAE PASS", "تسجيل الدخول بالهوية الرقمية".
- Title black or white only; logo only in its official colours; never recolour, crop, stretch or redraw the logo.
- Minimum size 140 × 30 px; margin around the button at least a tenth of its height.
- Black style on white or light backgrounds (white or outline style on dark ones).
- "Sign in" on the sign-in page only; never show sign-in and sign-up buttons on the same page.
- The corner radius may change (square or pill) to match other buttons; nothing else.
