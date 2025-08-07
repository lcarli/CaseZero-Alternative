export interface Language {
  code: string;
  name: string;
  flag: string;
}

export interface Translations {
  // Common
  loading: string;
  error: string;
  success: string;
  cancel: string;
  save: string;
  delete: string;
  edit: string;
  close: string;
  back: string;
  next: string;
  previous: string;
  
  // Navigation
  home: string;
  dashboard: string;
  login: string;
  register: string;
  logout: string;
  
  // Authentication
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
  firstName: string;
  lastName: string;
  department: string;
  badgeNumber: string;
  rank: string;
  specialization: string;
  
  // Authentication messages
  loginSuccess: string;
  loginError: string;
  registerSuccess: string;
  registerError: string;
  logoutSuccess: string;
  
  // Dashboard
  welcomeBack: string;
  availableCases: string;
  recentActivity: string;
  caseProgress: string;
  profile: string;
  settings: string;
  language: string;
  
  // Cases
  caseNumber: string;
  caseTitle: string;
  caseDescription: string;
  caseStatus: string;
  caseType: string;
  casePriority: string;
  caseAssigned: string;
  caseCreated: string;
  caseUpdated: string;
  
  // Case statuses
  statusOpen: string;
  statusInProgress: string;
  statusClosed: string;
  statusSuspended: string;
  
  // Case types
  homicide: string;
  theft: string;
  fraud: string;
  cybercrime: string;
  narcotics: string;
  
  // Case priorities
  priorityLow: string;
  priorityMedium: string;
  priorityHigh: string;
  priorityCritical: string;
  
  // Desktop
  evidence: string;
  suspects: string;
  witnesses: string;
  forensics: string;
  timeline: string;
  notes: string;
  
  // Evidence
  evidenceNumber: string;
  evidenceType: string;
  evidenceDescription: string;
  evidenceLocation: string;
  evidenceCollected: string;
  
  // About
  aboutTitle: string;
  aboutDescription: string;
  
  // Dashboard specific
  performanceStats: string;
  casesResolved: string;
  casesActive: string;
  successRate: string;
  averageRating: string;
  openWorkspace: string;
  weeklyGoals: string;
  recentHistory: string;
  lastSession: string;
  priority: string;
  
  // Footer
  currentLanguage: string;
  currentCase: string;
  
  // Departments
  homicideDept: string;
  theftDept: string;
  fraudDept: string;
  cybercrimeDept: string;
  narcoticsDept: string;
  
  // Ranks
  detective: string;
  inspector: string;
  sergeant: string;
  specialist: string;
  analyst: string;
  
  // Home page
  heroTitle: string;
  heroSubtitle: string;
  heroDescription: string;
  getStarted: string;
  learnMore: string;
  
  // Features
  featuresTitle: string;
  realisticInvestigation: string;
  realisticInvestigationDesc: string;
  evidenceAnalysis: string;
  evidenceAnalysisDesc: string;
  caseManagement: string;
  caseManagementDesc: string;
  
  // Feature list items
  authenticPoliceInterface: string;
  multipleCases: string;
  detectiveProgression: string;
  forensicAnalysis: string;
}

export const SUPPORTED_LANGUAGES: Language[] = [
  { code: 'pt-BR', name: 'Português (Brasil)', flag: '🇧🇷' },
  { code: 'en-US', name: 'English (United States)', flag: '🇺🇸' },
  { code: 'fr-FR', name: 'Français (France)', flag: '🇫🇷' },
  { code: 'es-ES', name: 'Español (España)', flag: '🇪🇸' },
];

export const DEFAULT_LANGUAGE = 'pt-BR';