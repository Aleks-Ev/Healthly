// Типы, повторяющие DTO бэкенда (HealthlyApi/Dtos). Меняются вместе с API.

export type Role = 'Patient' | 'Doctor' | 'Admin';
export const ROLES: Role[] = ['Patient', 'Doctor', 'Admin'];

export type DocumentStatus = 'Pending' | 'Processing' | 'Completed' | 'Error';
export const DOCUMENT_STATUSES: DocumentStatus[] = ['Pending', 'Processing', 'Completed', 'Error'];

export interface User {
  id: number;
  email: string;
  fullName: string;
  role: Role;
  height: number | null;
  weight: number | null;
  gender: string;
  phoneNumber: string;
  dateOfBirth: string | null; // "YYYY-MM-DD"
  city: string;
  createdAt: string;
}

// Ответ входа, регистрации и POST /api/auth/refresh
export interface AuthResponse {
  token: string; // access-токен (JWT), живёт 15 минут
  expiresAt: string;
  // Для POST /api/auth/refresh, живёт 30 дней; после обмена старый не действует.
  // Веб-клиент получает его в httpOnly-cookie, поэтому здесь null (мобильные клиенты — строку)
  refreshToken: string | null;
  refreshTokenExpiresAt: string;
  user: User;
}

export type Gender = 'Female' | 'Male' | 'Other';
export const GENDERS: Gender[] = ['Female', 'Male', 'Other'];

// Города Турции для сдачи анализов (TestingCities на бэкенде)
export const TESTING_CITIES = ['Istanbul', 'Ankara', 'Izmir', 'Antalya', 'Bursa', 'Adana', 'Gaziantep', 'Konya'] as const;
export type TestingCity = (typeof TESTING_CITIES)[number];

export interface RegisterRequest {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  phoneNumber: string; // "+90 532 123 45 67"
  dateOfBirth: string; // "YYYY-MM-DD"
  gender: Gender;
  city: TestingCity;
  accessCode?: string | null;
  marketingOptIn: boolean;
  acceptTerms: boolean;
  acceptMedicalAuthorization: boolean;
}

// PUT /api/auth/me/password
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  password: string;
  height?: number | null;
  weight?: number | null;
  gender?: string | null;
  role: Role;
}

// role и password отправляет только Admin; null — не менять
export interface UpdateUserRequest {
  email: string;
  fullName: string;
  height?: number | null;
  weight?: number | null;
  gender?: string | null;
  role?: Role | null;
  password?: string | null;
}

export interface MedicalDocument {
  id: number;
  userId: number;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadDate: string;
  status: DocumentStatus;
  resultsCount: number;
  abnormalCount: number;
}

export interface TestResult {
  id: number;
  medicalDocumentId: number;
  testName: string;
  value: string;
  unit: string;
  referenceRange: string;
  isAbnormal: boolean;
  // за критическим порогом справочника норм — срочно к врачу (красная подсветка)
  isCritical: boolean;
}

export interface MedicalDocumentDetails extends Omit<MedicalDocument, 'resultsCount' | 'abnormalCount'> {
  results: TestResult[];
}

// POST /api/documents/{id}/analyze — ИИ-анализ документа (DocumentAnalysisResponse на бэкенде)
export interface AnalyzedIndicator {
  testName: string;
  value: string;
  unit: string;
  referenceRange: string;
  // null — не удалось определить или значение похоже на ошибку распознавания (тогда confidence ≤ 0.4)
  isAbnormal: boolean | null;
  // за критическим порогом: опасно, нужен врач срочно (всегда и isAbnormal = true)
  isCritical: boolean;
  category: string | null;
  doctorSpecialty: string | null;
  confidence: number;
}

export interface DocumentAnalysis {
  documentId: number;
  indicators: AnalyzedIndicator[];
  abnormalCount: number;
  criticalCount: number;
  summary: string;
  recommendations: string[];
  suggestedSpecialists: string[];
  anonymizedText: string;
  model: string;
  isMock: boolean;
  analyzedAt: string;
  // null — черновик; иначе показатели подтверждены человеком и сохранены в историю
  confirmedAt: string | null;
  disclaimer: string;
}

// POST /api/documents/{id}/analysis/confirm — показатели, сверенные с бланком; заменяют все показатели документа
export interface ConfirmedIndicator {
  testName: string;
  value: string;
  unit?: string | null;
  referenceRange?: string | null;
}

export interface ConfirmAnalysisRequest {
  indicators: ConfirmedIndicator[];
}

// Значение ниже этой уверенности распознавания нужно сверить с бланком (DocumentAnalysisService.UncertainConfidence)
export const UNCERTAIN_CONFIDENCE = 0.4;

// isAbnormal = null — сервер определит по referenceRange
export interface TestResultRequest {
  testName: string;
  value: string;
  unit?: string | null;
  referenceRange?: string | null;
  isAbnormal?: boolean | null;
}

export interface ReportIndicator {
  testName: string;
  latestValue: string;
  unit: string;
  referenceRange: string;
  isAbnormal: boolean;
  isCritical: boolean;
  measuredAt: string;
  measurementsCount: number;
  documentId: number;
}

export interface UserReport {
  user: User;
  documentsCount: number;
  resultsCount: number;
  abnormalCount: number;
  lastUploadDate: string | null;
  indicators: ReportIndicator[];
}

// GET /api/users/{id}/trends — только числовые значения, точки по возрастанию даты
export interface TrendPoint {
  documentId: number;
  measuredAt: string;
  value: number;
  isAbnormal: boolean;
  isCritical: boolean;
}

export interface IndicatorTrend {
  testName: string;
  unit: string;
  referenceRange: string;
  referenceMin: number | null;
  referenceMax: number | null;
  points: TrendPoint[];
}
