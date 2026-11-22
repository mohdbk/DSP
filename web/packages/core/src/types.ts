// Shapes returned by the DSP APIs (v1). One definition drives forms in both portals.

/** Every list endpoint returns one page in this shape. */
export interface PagedList<T> { items: T[]; page: number; pageSize: number; total: number }

export type FieldType =
  | 'text' | 'textarea' | 'email' | 'phone' | 'number' | 'date' | 'select' | 'file' | 'checkbox';

export const FIELD_TYPES: FieldType[] = ['text', 'textarea', 'email', 'phone', 'number', 'date', 'select', 'file', 'checkbox'];

export const PROFILE_CLAIMS = ['name', 'emirates_id', 'email', 'mobile'] as const;

export interface FieldOption { value: string; label: string; labelAr?: string | null }

export interface FieldDefinition {
  key: string;
  label: string;
  labelAr?: string | null;
  type: FieldType;
  required: boolean;
  help?: string | null;
  helpAr?: string | null;
  placeholder?: string | null;
  placeholderAr?: string | null;
  options?: FieldOption[] | null;
  minLength?: number | null;
  maxLength?: number | null;
  min?: number | null;
  max?: number | null;
  pattern?: string | null;
  patternMessage?: string | null;
  patternMessageAr?: string | null;
  prefillFrom?: string | null;
}

export interface FormStep {
  id: string;
  title: string;
  titleAr?: string | null;
  description?: string | null;
  descriptionAr?: string | null;
  fields: FieldDefinition[];
}

export interface ServiceDefinition {
  code: string;
  name: string;
  nameAr?: string | null;
  description: string;
  descriptionAr?: string | null;
  category: string;
  categoryAr?: string | null;
  fee: number;
  currency: string;
  referenceField?: string | null;
  version?: number;
  workflowVersion: number;
  reviewChecklist?: string[] | null;
  steps: FormStep[];
}

export interface ServiceSummary {
  code: string;
  name: string;
  nameAr?: string | null;
  description: string;
  descriptionAr?: string | null;
  category: string;
  categoryAr?: string | null;
  fee: number;
  currency: string;
  stepCount: number;
  version: number;
  workflowVersion: number;
}

export type ApplicationStatus =
  | 'Draft' | 'Submitted' | 'AiReview' | 'InformationRequested' | 'UnderReview' | 'AwaitingPayment' | 'Paid'
  | 'Approved' | 'Rejected';

export interface LocalizedText { en: string; ar: string }

export type FormData = Record<string, string | null | undefined>;

export interface Application {
  id: string;
  number: string;
  serviceCode: string;
  serviceName: string;
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
  serviceName: string;
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

export interface AiFinding { severity: 'issue' | 'warning' | 'info'; field?: string | null; message: string }

export interface AiAssessment {
  id: string;
  round: number;
  recommendation: AiRecommendation;
  summary: string;
  findings: AiFinding[];
  questions: LocalizedText[];
  mode: 'llm' | 'rules' | 'unavailable';
  model?: string | null;
  createdAt: string;
}

// Service builder

export interface BuilderServiceSummary {
  code: string;
  name: string;
  nameAr?: string | null;
  category: string;
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
  description: string;
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
  title: string;
  body: string;
  titleAr?: string | null;
  bodyAr?: string | null;
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
