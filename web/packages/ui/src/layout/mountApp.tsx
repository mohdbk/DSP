import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider, type AnyRouter } from '@tanstack/react-router';
import '../styles.css';

/** Starts a portal: the design system styles and the portal's router in #root. */
export function mountApp(router: AnyRouter) {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <RouterProvider router={router} />
    </StrictMode>,
  );
}
