// @dsp/ui — the DSP component library, shared by the citizen portal and the back office.
// Built on the UAE Design System (AEGOV); React Aria and TanStack Table fill the gaps AEGOV leaves
// (tables, reorderable lists). Pages compose these; they don't define their own components.

// Layout and navigation
export * from './layout/AppShell';
export * from './layout/Page';
export * from './layout/PortalFrame';
export * from './layout/mountApp';
export * from './navigation/LanguageSwitch';
export * from './navigation/AccountArea';
export * from './identity/UaePassButton';
export * from './navigation/useInternalLinks';
export * from './navigation/routing';

// Feedback
export * from './feedback/Feedback';

// Forms
export * from './forms/fields';
export * from './forms/MultilingualField';
export * from './forms/MarkdownEditor';
export * from './forms/ServiceForm';

// Data
export * from './data/DataTable';
export * from './data/DescriptionList';
export * from './data/ReorderList';

// Domain views
export * from './application/ApplicationViews';
export * from './application/ApplicationCase';
export * from './ai/AiReviewPanel';
export * from './chat/ChatThread';

// AEGOV primitives the pages use directly (one import path for everything visual).
export {
  AegovButton, AegovLink, AegovTabs, AegovTab, AegovTabPanel, AegovBadge,
} from '@aegov/designsystem-react';
