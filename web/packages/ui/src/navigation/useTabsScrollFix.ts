import { useCallback } from 'react';

/**
 * AEGOV's tab list scrolls sideways (overflow-x: auto), which also lets it scroll a few pixels up and down on touch
 * screens and hides the active tab's underline. Pass the returned ref to <AegovTabs ref={…}>: it stops the list
 * scrolling vertically and gives the underline room. The list is in the component's shadow root, out of reach of
 * page CSS, so this sets the style on it directly once the component has rendered.
 */
export function useTabsScrollFix() {
  return useCallback((host: HTMLElement | null) => {
    if (!host) return;
    const fix = () => {
      host.shadowRoot?.querySelectorAll<HTMLElement>('*').forEach(el => {
        const overflowX = getComputedStyle(el).overflowX;
        if (overflowX === 'auto' || overflowX === 'scroll') {
          el.style.overflowY = 'hidden';
          el.style.paddingBottom = '3px';
        }
      });
    };
    fix();
    requestAnimationFrame(fix);
    setTimeout(fix, 300);
  }, []);
}
