// Shapes returned by the DSP APIs (v1). One definition drives forms in both portals. Human-facing texts of
// services are LocalizedText maps (any number of languages); show them with loc().

import type { LocalizedText } from './i18n';

/** Every list endpoint returns one page in this shape. */
export interface PagedList<T> { items: T[]; page: number; pageSize: number; total: number }

export type FieldType =
  | 'text' | 'textarea' | 'email' | 'phone' | 'number' | 'date' | 'select' | 'file' | 'checkbox';

export const FIELD_TYPES: FieldType[] = ['text', 'textarea', 'email', 'phone', 'number', 'date', 'select', 'file', 'checkbox'];

export const PROFILE_CLAIMS = ['name', 'emirates_id', 'email', 'mobile'] as const;

export interface FieldOption { value: string; label: LocalizedText }

export interface FieldDefinition {
  key: string;
  label: LocalizedText;
  type: FieldType;
  required: boolean;
  help?: LocalizedText | null;
  placeholder?: LocalizedText | null;
  options?: FieldOption[] | null;
  minLength?: number | null;
  maxLength?: number | null;
  min?: number | null;
  max?: number | null;
  pattern?: string | null;
  patternMessage?: LocalizedText | null;
  prefillFrom?: string | null;
}

export interface FormStep {
  id: string;
  title: LocalizedText;
  description?: LocalizedText | null;
  fields: FieldDefinition[];
}

export interface ServiceDefinition {
  code: string;
  name: LocalizedText;
  description: LocalizedText;
  category: LocalizedText;
  fee: number;
  currency: string;
  referenceField?: string | null;
  version?: number;
  workflowVersion: number;
  reviewChecklist?: LocalizedText[] | null;
  steps: FormStep[];
}

export interface ServiceSummary {
  code: string;
  name: LocalizedText;
  description: LocalizedText;
  category: LocalizedText;
  fee: number;
  currency: string;
  stepCount: number;
  version: number;
  workflowVersion: number;
}

export type ApplicationStatus =
  | 'Draft' | 'Submitted' | 'AiReview' | 'InformationRequested' | 'UnderReview' | 'AwaitingPayment' | 'Paid'
  | 'Approved' | 'Rejected';

export type FormData = Record<string, string | null | undefined>;

export interface Application {
  id: string;
  number: string;
  serviceCode: string;
  serviceName: LocalizedText;
  serviceVersion: number;
  workflowVersion: number;
  applicantId: string;
  applicantName: string;
  status: ApplicationStatus;
  data: FormData;
  referenceValue?: string | null;
  fee: number;
  currency: string;
  createdAt: string;
  updatedAt: string;
  submittedAt?: string | null;
  completedAt?: string | null;
  approved?: boolean | null;
  decisionComment?: string | null;
  paymentId?: string | null;
  certificateDocumentId?: string | null;
  informationRequest: LocalizedText[];
  informationRequestCount: number;
}

export interface FieldError { field: string; message: string }

export interface SaveResult { application: Application; errors: FieldError[] }

export interface ReviewTask {
  id: string;
  applicationId: string;
  applicationNumber: string;
  serviceCode: string;
  serviceName: LocalizedText;
  applicantName: string;
  referenceValue?: string | null;
  status: 'Open' | 'Completed';
  createdAt: string;
  dueAt: string;
  completedAt?: string | null;
  outcome?: string | null;
  aiRecommendation?: AiRecommendation | null;
}

export type AiRecommendation = 'approve' | 'request_information' | 'reject' | 'unavailable';

export interface AiFinding { severity: 'issue' | 'warning' | 'info'; field?: string | null; message: LocalizedText }

export interface AiAssessment {
  id: string;
  round: number;
  recommendation: AiRecommendation;
  summary: LocalizedText;
  findings: AiFinding[];
  questions: LocalizedText[];
  mode: 'llm' | 'rules' | 'unavailable';
  model?: string | null;
  createdAt: string;
}

// Service builder

export interface BuilderServiceSummary {
  code: string;
  name: LocalizedText;
  category: LocalizedText;
  publishedVersion?: number | null;
  draftVersion?: number | null;
  workflowVersion: number;
  updatedAt: string;
  updatedBy: string;
}

export interface ServiceVersionInfo {
  version: number;
  status: 'Draft' | 'Published' | 'Retired';
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  publishedAt?: string | null;
  publishedBy?: string | null;
}

export interface BuilderService {
  code: string;
  published?: ServiceDefinition | null;
  draft?: ServiceDefinition | null;
  draftErrors: FieldError[];
  versions: ServiceVersionInfo[];
}

export interface Payment {
  id: string;
  referenceId: string;
  description: LocalizedText;
  amount: number;
  currency: string;
  status: 'Pending' | 'Paid';
  createdAt: string;
  paidAt?: string | null;
  method?: string | null;
  transactionReference?: string | null;
}

export interface DocumentInfo {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  kind: string;
  createdAt: string;
}

export interface Notification {
  id: string;
  /** In the reader's language (rendered by the server from Accept-Language). */
  title: string;
  body: string;
  link?: string | null;
  createdAt: string;
  isRead: boolean;
}

export interface ChatMessage { role: 'user' | 'assistant'; content: string }

export interface ChatResponse {
  reply: string;
  pendingField?: string | null;
  application: Pick<Application, 'id' | 'number' | 'serviceCode' | 'serviceName' | 'status' | 'data'>;
  mode: 'llm' | 'guided';
}

// Platform: languages, texts and AI settings (back office)

export interface LanguageAdmin {
  code: string;
  name: LocalizedText;
  direction: 'ltr' | 'rtl';
  isEnabled: boolean;
  isDefault: boolean;
  sortOrder: number;
  translated: number;
  total: number;
  updatedAt: string;
  updatedBy: string;
}

export interface TranslationRow {
  key: string;
  defaultText: string;
  text?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
  /** Where the key is used: 'portal', 'backoffice' and/or 'services' (messages from the APIs). Empty: added by staff or unused. */
  usedIn: string[];
}

export interface AiSettingsView {
  providerName: string;
  baseUrl: string;
  model: string;
  hasApiKey: boolean;
  temperature: number;
  timeoutSeconds: number;
  chatEnabled: boolean;
  reviewEnabled: boolean;
  maxToolIterations: number;
  maxHistoryMessages: number;
  chatInstructions?: string | null;
  reviewInstructions?: string | null;
  /** Staff's prompt; null means the shipped default is in force. */
  chatPrompt?: string | null;
  reviewPrompt?: string | null;
  defaultChatPrompt: string;
  defaultReviewPrompt: string;
  isConfigured: boolean;
  updatedAt: string;
  updatedBy: string;
}

export interface AiTestResult { ok: boolean; messageKey: string; detail?: string | null; latencyMs?: number | null }
