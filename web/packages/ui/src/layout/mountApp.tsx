import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider, type AnyRouter } from '@tanstack/react-router';
import { initI18n, type PortalApp } from '@dsp/core';
import '../styles.css';

/**
 * Starts a portal: loads the enabled languages and the reader's texts from the Platform service, then renders the
 * portal's router in #root with the design system styles.
 */
export function mountApp(router: AnyRouter, app: PortalApp) {
  void initI18n(app).finally(() => {
    createRoot(document.getElementById('root')!).render(
      <StrictMode>
        <RouterProvider router={router} />
      </StrictMode>,
    );
  });
}
