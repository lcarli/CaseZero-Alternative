const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

// Types matching the backend DTOs
export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  firstName: string
  lastName: string
  personalEmail: string
  password: string
}

export interface LoginResponse {
  token: string
  user: User
}

export interface User {
  id: string
  firstName: string
  lastName: string
  email: string
  personalEmail: string
  department?: string
  position?: string
  badgeNumber?: string
  emailVerified: boolean
}

export interface Case {
  id: string
  title: string
  description?: string
  status: 'Open' | 'InProgress' | 'Resolved' | 'Closed'
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  createdAt: string
  closedAt?: string
  assignedUsers: User[]
  userProgress?: CaseProgress
}

export interface CaseProgress {
  userId: string
  caseId: string
  evidencesCollected: number
  interviewsCompleted: number
  reportsSubmitted: number
  lastActivity: string
  completionPercentage: number
}

export interface Dashboard {
  stats: UserStats
  cases: Case[]
  recentActivities: RecentActivity[]
}

export interface UserStats {
  casesResolved: number
  casesActive: number
  successRate: number
  averageRating: number
}

export interface RecentActivity {
  description: string
  date: string
  caseId?: string
}

export interface CaseSession {
  id: number
  userId: string
  caseId: string
  sessionStart: string
  sessionEnd?: string
  sessionDurationMinutes: number
  gameTimeAtStart?: string
  gameTimeAtEnd?: string
  isActive: boolean
}

export interface StartCaseSessionRequest {
  caseId: string
  gameTimeAtStart?: string
}

export interface EndCaseSessionRequest {
  gameTimeAtEnd?: string
}

export interface VerifyEmailRequest {
  token: string
}

export interface ResendVerificationRequest {
  email: string
}

export interface GeneratePoliceEmailRequest {
  firstName: string
  lastName: string
}

export interface GeneratePoliceEmailResponse {
  policeEmail: string
}

export interface GenerateCaseRequest {
  title: string
  location: string
  incidentDateTime: string
  pitch: string
  twist: string
  difficulty?: string
  targetDurationMinutes?: number
  constraints?: string
  timezone?: string
  generateImages?: boolean
}

export interface SimpleCaseRequest {
  difficulty: string
}

export interface CasePackage {
  caseJson: string
  generatedDocs: any[]
  imagePrompts: any[]
  evidenceManifest: any
}

class ApiError extends Error {
  public status: number
  
  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

// Token management
const TOKEN_KEY = 'casezero_token'

export const tokenStorage = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_KEY, token),
  remove: () => localStorage.removeItem(TOKEN_KEY)
}

// Base fetch function with token handling
const apiFetch = async (endpoint: string, options: RequestInit = {}) => {
  const url = `${API_BASE_URL}${endpoint}`
  const token = tokenStorage.get()
  
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string> || {})
  }
  
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }
  
  const response = await fetch(url, {
    ...options,
    headers
  })
  
  if (!response.ok) {
    let errorMessage = `HTTP ${response.status}: ${response.statusText}`
    try {
      const errorData = await response.json()
      errorMessage = errorData.message || errorData.title || errorMessage
    } catch {
      // If we can't parse error as JSON, use the default message
    }
    throw new ApiError(response.status, errorMessage)
  }
  
  return response.json()
}

// Authentication API
export const authApi = {
  login: async (credentials: LoginRequest): Promise<LoginResponse> => {
    const response = await apiFetch('/auth/login', {
      method: 'POST',
      body: JSON.stringify(credentials)
    })
    
    // Store the token
    tokenStorage.set(response.token)
    
    return response
  },
  
  register: async (userData: RegisterRequest): Promise<{ message: string, policeEmail: string, personalEmail: string }> => {
    return apiFetch('/auth/register', {
      method: 'POST',
      body: JSON.stringify(userData)
    })
  },

  verifyEmail: async (request: VerifyEmailRequest): Promise<{ message: string }> => {
    return apiFetch('/auth/verify-email', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  },

  resendVerification: async (request: ResendVerificationRequest): Promise<{ message: string }> => {
    return apiFetch('/auth/resend-verification', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  },

  generatePoliceEmail: (firstName: string, lastName: string): string => {
    // Client-side generation to match server logic
    const cleanFirstName = firstName.trim().split(' ')[0].toLowerCase()
      .normalize('NFD').replace(/[\u0300-\u036f]/g, '') // Remove accents
    const cleanLastName = lastName.trim().split(' ').pop()?.toLowerCase()
      .normalize('NFD').replace(/[\u0300-\u036f]/g, '') || '' // Remove accents
    
    return `${cleanFirstName}.${cleanLastName}@fic-police.gov`
  },
  
  logout: () => {
    tokenStorage.remove()
  },
  
  isAuthenticated: () => {
    return !!tokenStorage.get()
  }
}

// Cases API
export const casesApi = {
  getDashboard: async (): Promise<Dashboard> => {
    return apiFetch('/cases/dashboard')
  },
  
  getCases: async (): Promise<Case[]> => {
    return apiFetch('/cases')
  },
  
  getCase: async (id: string): Promise<Case> => {
    return apiFetch(`/cases/${id}`)
  },
  
  getCaseData: async (id: string): Promise<any> => {
    return apiFetch(`/cases/${id}/data`)
  }
}

// Case Session API
export const caseSessionApi = {
  startSession: async (request: StartCaseSessionRequest): Promise<CaseSession> => {
    return apiFetch('/casesession/start', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  },
  
  endSession: async (caseId: string, request: EndCaseSessionRequest): Promise<CaseSession> => {
    return apiFetch(`/casesession/end/${caseId}`, {
      method: 'POST',
      body: JSON.stringify(request)
    })
  },
  
  getLastSession: async (caseId: string): Promise<CaseSession> => {
    return apiFetch(`/casesession/last/${caseId}`)
  },
  
  getCaseSessions: async (caseId: string): Promise<CaseSession[]> => {
    return apiFetch(`/casesession/${caseId}`)
  }
}

// CaseObject API - for accessing case files
export const caseObjectApi = {
  getCaseFile: async (caseId: string, fileName: string): Promise<Blob> => {
    const url = `${API_BASE_URL}/caseobject/${caseId}/files/${fileName}`
    const token = tokenStorage.get()
    
    const response = await fetch(url, {
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      }
    })
    
    if (!response.ok) {
      throw new ApiError(response.status, `Failed to load file: ${fileName}`)
    }
    
    return response.blob()
  },

  getCaseFileUrl: (caseId: string, fileName: string): string => {
    const token = tokenStorage.get()
    const url = `${API_BASE_URL}/caseobject/${caseId}/files/${fileName}`
    // For images, we can return the URL directly since the browser will handle authentication
    return token ? `${url}?token=${token}` : url
  }
}

// Case Generation API
export const caseGenerationApi = {
  generateCase: async (request: GenerateCaseRequest): Promise<CasePackage> => {
    return apiFetch('/casegeneration/generate', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  },
  
  generateCaseJson: async (request: GenerateCaseRequest): Promise<{ caseJson: string }> => {
    return apiFetch('/casegeneration/generate-json', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  },
  
  generateSimpleCase: async (request: SimpleCaseRequest): Promise<CasePackage> => {
    return apiFetch('/casegeneration/generate-simple', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  },
  
  generateSimpleCaseJson: async (request: SimpleCaseRequest): Promise<{ caseJson: string }> => {
    return apiFetch('/casegeneration/generate-simple-json', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  }
}

// Case Files API (for normalized case bundles)
export interface FileViewerItem {
  id: string
  name: string
  title?: string // Display title
  type: string
  icon: string
  category: string
  size: string
  sizeBytes?: number
  modified: string
  timestamp?: string // Alternative to modified
  content: string
  evidenceId?: string
  author?: string
  isUnlocked: boolean
  mediaUrl?: string // URL for media files (images)
}

export interface CaseFilesResponse {
  caseId: string
  files: FileViewerItem[]
  totalFiles: number
  filesByCategory: Record<string, number>
}

export interface CaseDocument {
  docId: string
  type: string
  title: string
  words: number
  sections: DocumentSection[]
}

export interface DocumentSection {
  title: string
  content: string
}

export interface NormalizedCaseBundle {
  caseId: string
  version: string
  generatedAt: string
  entities: {
    suspects: string[]
    evidence: string[]
    witnesses: string[]
    total: number
  }
  documents: {
    items: string[]
    total: number
  }
  context: {
    plan: Record<string, string>
    expand: Record<string, string>
  }
}

export interface ForensicRequestDTO {
  id?: number
  caseId: string
  evidenceId: string
  evidenceName: string
  analysisType: 'DNA' | 'Fingerprint' | 'DigitalForensics' | 'Ballistics'
  requestedAt: string // ISO string
  estimatedCompletionTime: string // ISO string
  completedAt?: string // ISO string
  status: 'pending' | 'in-progress' | 'completed' | 'cancelled'
  resultDocumentId?: string
  notes?: string
}

export const caseFilesApi = {
  /**
   * Get all files for a specific case from the normalized bundle
   */
  getCaseFiles: async (caseId: string): Promise<CaseFilesResponse> => {
    return apiFetch(`/casefiles/${caseId}`)
  },

  /**
   * Get a specific document from a case
   */
  getCaseDocument: async (caseId: string, documentId: string): Promise<CaseDocument> => {
    return apiFetch(`/casefiles/${caseId}/documents/${documentId}`)
  },

  /**
   * Get the normalized case bundle
   */
  getCaseBundle: async (caseId: string): Promise<NormalizedCaseBundle> => {
    return apiFetch(`/casefiles/${caseId}/bundle`)
  }
}

export const forensicRequestApi = {
  /**
   * Get all forensic requests for a case
   */
  getForensicRequests: async (caseId: string): Promise<ForensicRequestDTO[]> => {
    return apiFetch(`/forensicrequest/${caseId}`)
  },

  /**
   * Get pending forensic requests for a case
   */
  getPendingForensicRequests: async (caseId: string): Promise<ForensicRequestDTO[]> => {
    return apiFetch(`/forensicrequest/${caseId}/pending`)
  },

  /**
   * Get a specific forensic request
   */
  getForensicRequest: async (caseId: string, id: number): Promise<ForensicRequestDTO> => {
    return apiFetch(`/forensicrequest/${caseId}/${id}`)
  },

  /**
   * Create a new forensic request
   */
  createForensicRequest: async (request: ForensicRequestDTO): Promise<ForensicRequestDTO> => {
    return apiFetch('/forensicrequest', {
      method: 'POST',
      body: JSON.stringify(request),
      headers: {
        'Content-Type': 'application/json'
      }
    })
  },

  /**
   * Update a forensic request
   */
  updateForensicRequest: async (caseId: string, id: number, request: ForensicRequestDTO): Promise<void> => {
    return apiFetch(`/forensicrequest/${caseId}/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
      headers: {
        'Content-Type': 'application/json'
      }
    })
  },

  /**
   * Delete a forensic request
   */
  deleteForensicRequest: async (caseId: string, id: number): Promise<void> => {
    return apiFetch(`/forensicrequest/${caseId}/${id}`, {
      method: 'DELETE'
    })
  }
}

export { ApiError }

