// Shapes returned by the DSP APIs. One definition drives forms in both portals.

export type FieldType =
  | 'text' | 'textarea' | 'email' | 'phone' | 'number' | 'date' | 'select' | 'file' | 'checkbox';

export interface FieldOption { value: string; label: string }

export interface FieldDefinition {
  key: string;
  label: string;
  type: FieldType;
  required: boolean;
  help?: string | null;
  placeholder?: string | null;
  options?: FieldOption[] | null;
  minLength?: number | null;
  maxLength?: number | null;
  min?: number | null;
  max?: number | null;
  pattern?: string | null;
  patternMessage?: string | null;
}

export interface FormStep {
  id: string;
  title: string;
  description?: string | null;
  fields: FieldDefinition[];
}

export interface ServiceDefinition {
  code: string;
  name: string;
  nameAr?: string | null;
  description: string;
  category: string;
  fee: number;
  currency: string;
  referenceField?: string | null;
  workflowVersion: number;
  steps: FormStep[];
}

export interface ServiceSummary {
  code: string;
  name: string;
  nameAr?: string | null;
  description: string;
  category: string;
  fee: number;
  currency: string;
  stepCount: number;
}

export type ApplicationStatus =
  | 'Draft' | 'Submitted' | 'UnderReview' | 'AwaitingPayment' | 'Paid' | 'Approved' | 'Rejected';

export type FormData = Record<string, string | null | undefined>;

export interface Application {
  id: string;
  number: string;
  serviceCode: string;
  serviceName: string;
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
