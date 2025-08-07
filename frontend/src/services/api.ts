const API_BASE_URL = 'http://localhost:5000/api'

// Types matching the backend DTOs
export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  firstName: string
  lastName: string
  email: string
  phoneNumber: string
  department: string
  position: string
  badgeNumber: string
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
  department?: string
  position?: string
  badgeNumber?: string
  isApproved: boolean
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
  
  register: async (userData: RegisterRequest): Promise<{ message: string }> => {
    return apiFetch('/auth/register', {
      method: 'POST',
      body: JSON.stringify(userData)
    })
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

export { ApiError }