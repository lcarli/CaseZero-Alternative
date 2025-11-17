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
  divisionHeatmap: string;
  evidenceProgress: string;
  commandBulletins: string;
  noRecentActivity: string;
  noDataAvailable: string;
  viewDossier: string;
  
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
  
  // Login page specific
  systemAccess: string;
  metropolitanPoliceDept: string;
  coldCaseUnit: string;
  emailOrId: string;
  enterPassword: string;
  enterSystem: string;
  authenticating: string;
  noAccess: string;
  requestRegistration: string;
  testCredentials: string;
  backToHome: string;
  loginBadgeSecure: string;
  loginBadgeInternal: string;
  loginBadgeMonitored: string;
  loginSecurityTitle: string;
  loginSecurityDesc: string;
  loginSupportTitle: string;
  loginSupportDesc: string;
  
  // Register page specific
  registrationRequest: string;
  registrationFormSubtitle: string;
  registrationChecklistTitle: string;
  registrationChecklistDesc: string;
  registrationChecklistItem1: string;
  registrationChecklistItem2: string;
  registrationChecklistItem3: string;
  registrationChecklistItem4: string;
  registrationSupportTitle: string;
  registrationSupportDesc: string;
  importantNote: string;
  systemRestricted: string;
  institutionalEmail: string;
  phoneNumber: string;
  badgeNumberField: string;
  selectOption: string;
  investigationDivision: string;
  criminalForensics: string;
  cybercrimes: string;
  homicides: string;
  frauds: string;
  position: string;
  minimumChars: string;
  confirmYourPassword: string;
  sendingRequest: string;
  requestRegistrationBtn: string;
  alreadyHaveAccess: string;
  doLogin: string;
  passwordsDontMatch: string;
  registrationSent: string;
  unexpectedError: string;
  
  // Error handling
  networkError: string;
  serverError: string;
  notFoundError: string;
  unauthorizedError: string;
  forbiddenError: string;
  validationError: string;
  timeoutError: string;
  genericError: string;
  errorOccurred: string;
  tryAgain: string;
  
  // Loading states
  loadingData: string;
  loadingCases: string;
  loadingProfile: string;
  processing: string;
  saving: string;
  uploading: string;
  pleaseWait: string;
  almostDone: string;
  
  // Offline support
  offline: string;
  online: string;
  offlineMessage: string;
  connectionRestored: string;
  workingOffline: string;
  
  // Keyboard navigation
  keyboardShortcuts: string;
  pressEscToClose: string;
  pressEnterToSelect: string;
  useArrowKeys: string;
  pressTabToNavigate: string;
  
  // New landing page content
  digitalInterface: string;
  careerProgression: string;
  justiceSystem: string;
  justiceSystemDesc: string;
  landingBadgeSecurity: string;
  landingBadgeCompliance: string;
  landingBadgeInternalUse: string;
  landingHighlightsTitle: string;
  landingHighlightEvidenceTitle: string;
  landingHighlightEvidenceDesc: string;
  landingHighlightCollaborationTitle: string;
  landingHighlightCollaborationDesc: string;
  landingHighlightAutomationTitle: string;
  landingHighlightAutomationDesc: string;
  landingFeatureIntelligenceTitle: string;
  landingFeatureIntelligenceDesc: string;
  landingFeatureCoordinationTitle: string;
  landingFeatureCoordinationDesc: string;
  landingFeatureEvidenceTitle: string;
  landingFeatureEvidenceDesc: string;
  landingFeatureTrainingTitle: string;
  landingFeatureTrainingDesc: string;
  landingProcessTitle: string;
  landingProcessDescription: string;
  landingProcessStepIntakeTitle: string;
  landingProcessStepIntakeDesc: string;
  landingProcessStepAnalysisTitle: string;
  landingProcessStepAnalysisDesc: string;
  landingProcessStepDecisionTitle: string;
  landingProcessStepDecisionDesc: string;
  landingCTAHeadline: string;
  landingCTADescription: string;
  
  // Game mechanics
  howItWorks: string;
  howItWorksDesc: string;
  investigation: string;
  investigationDesc: string;
  forensicsDesc: string;
  documentation: string;
  documentationDesc: string;
  realTime: string;
  realTimeDesc: string;
  
  // Call to action
  readyToSolve: string;
  ctaDescription: string;
  accessSystem: string;
  requestAccess: string;
  
  // New registration form translations
  newRegistrationInfo: string;
  institutionalEmailPreview: string;
  personalEmail: string;
  submitting: string;
  requestRegistrationBtn2: string;
  institutionalEmailInfo: string;
  useInstitutionalLogin: string;
  
  // Evidence visibility and forensics
  evidenceVisibility: string;
  visibleEvidences: string;
  hiddenEvidences: string;
  makeVisible: string;
  makeHidden: string;
  visibilityUpdated: string;
  forensicAnalysisTitle: string;
  requestAnalysis: string;
  analysisType: string;
  analysisInProgress: string;
  analysisCompleted: string;
  analysisResults: string;
  noAnalysisAvailable: string;
  timeBasedAnalysis: string;
  estimatedCompletion: string;
  analysisTypeNotSupported: string;
  selectAnalysisType: string;
  supportedAnalyses: string;
  
  // Analysis types
  dnaAnalysis: string;
  fingerprintAnalysis: string;
  digitalForensics: string;
  ballisticsAnalysis: string;
  toxicologyAnalysis: string;
  handwritingAnalysis: string;
  voiceAnalysis: string;
  traceAnalysis: string;
  generalAnalysis: string;
  
  // Case processing
  caseProcessing: string;
  processingAllCases: string;
  processingCase: string;
  caseProcessed: string;
  caseAlreadyProcessed: string;
  processingStatus: string;
  
  // Rank-based access
  accessDenied: string;
  insufficientRank: string;
  rankRequired: string;
  yourRank: string;
  availableForYourRank: string;
  
  // Generate Case
  generateCase: string;
  generateNewCase: string;
  caseGeneratorAI: string;
  caseGeneration: string;
  caseLocation: string;
  incidentDateTime: string;
  casePitch: string;
  caseTwist: string;
  caseDifficulty: string;
  targetDuration: string;
  constraints: string;
  timezone: string;
  generateImages: string;
  generateCaseBtn: string;
  generatingCase: string;
  caseGeneratedSuccess: string;
  caseGenerationError: string;
  fillRequiredFields: string;
  easy: string;
  medium: string;
  hard: string;
  caseGenerationForm: string;
  caseGenerationDesc: string;
  caseGenerationSimpleDesc: string;
  backToDashboard: string;
  selectDifficulty: string;
  difficultyHelpText: string;
  // Case Generator AI
  caseGeneratorTitle: string;
  caseGeneratorDescription: string;
  startGeneration: string;
  generationProgress: string;
  currentStep: string;
  estimatedTime: string;
  completed: string;
  failed: string;
  running: string;
  // Generation Steps
  stepPlan: string;
  stepExpand: string;
  stepDesign: string;
  stepGenDocs: string;
  stepGenMedia: string;
  stepNormalize: string;
  stepIndex: string;
  stepRuleValidate: string;
  stepRedTeam: string;
  stepPackage: string;
  
  // Pinboard
  pinboard: string;
  detectivePinboard: string;
  pinboardTitle: string;
  addEvidence: string;
  addNote: string;
  addPhoto: string;
  evidenceItem: string;
  noteItem: string;
  photoItem: string;
  dragToOrganize: string;
  connectEvidence: string;
  clearBoard: string;
  saveBoard: string;
  loadBoard: string;
  evidenceConnected: string;
  evidenceDisconnected: string;
  boardCleared: string;
  boardSaved: string;
  boardLoaded: string;
  deleteItem: string;
  editItem: string;
  itemDeleted: string;
  noItemsOnBoard: string;
  dropHereToAdd: string;
}

export const SUPPORTED_LANGUAGES: Language[] = [
  { code: 'pt-BR', name: 'Português (Brasil)', flag: '🇧🇷' },
  { code: 'en-US', name: 'English (United States)', flag: '🇺🇸' },
  { code: 'fr-FR', name: 'Français (France)', flag: '🇫🇷' },
  { code: 'es-ES', name: 'Español (España)', flag: '🇪🇸' },
];

export const DEFAULT_LANGUAGE = 'pt-BR';