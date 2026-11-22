import { useEffect } from 'react';

/**
 * AEGOV links and navbar items render real anchors inside their shadow roots. This keeps same-site clicks
 * inside the single-page app (no full reload) by handing them to the router. API and sign-in URLs still
 * load normally.
 */
export function useInternalLinks(navigate: (href: string) => void) {
  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      const anchor = e.composedPath().find((n): n is HTMLAnchorElement => n instanceof HTMLAnchorElement);
      if (!anchor?.href || (anchor.target && anchor.target !== '_self') || anchor.hasAttribute('download')) return;
      const url = new URL(anchor.href, location.href);
      if (url.origin !== location.origin || url.pathname.startsWith('/api/')) return;
      e.preventDefault();
      navigate(url.pathname + url.search + url.hash);
    }
    document.addEventListener('click', onClick);
    return () => document.removeEventListener('click', onClick);
  }, [navigate]);
}
