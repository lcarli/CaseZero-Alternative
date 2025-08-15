import type { Translations } from '../types/i18n';

export const enUS: Translations = {
  // Common
  loading: 'Loading...',
  error: 'Error',
  success: 'Success',
  cancel: 'Cancel',
  save: 'Save',
  delete: 'Delete',
  edit: 'Edit',
  close: 'Close',
  back: 'Back',
  next: 'Next',
  previous: 'Previous',
  
  // Navigation
  home: 'Home',
  dashboard: 'Dashboard',
  login: 'Login',
  register: 'Register',
  logout: 'Logout',
  
  // Authentication
  username: 'Username',
  email: 'Email',
  password: 'Password',
  confirmPassword: 'Confirm password',
  firstName: 'First name',
  lastName: 'Last name',
  department: 'Department',
  badgeNumber: 'Badge number',
  rank: 'Rank',
  specialization: 'Specialization',
  
  // Authentication messages
  loginSuccess: 'Login successful',
  loginError: 'Login error',
  registerSuccess: 'Registration successful',
  registerError: 'Registration error',
  logoutSuccess: 'Logout successful',
  
  // Dashboard
  welcomeBack: 'Welcome back',
  availableCases: 'Available cases',
  recentActivity: 'Recent activity',
  caseProgress: 'Case progress',
  profile: 'Profile',
  settings: 'Settings',
  language: 'Language',
  
  // Cases
  caseNumber: 'Case number',
  caseTitle: 'Case title',
  caseDescription: 'Case description',
  caseStatus: 'Case status',
  caseType: 'Case type',
  casePriority: 'Case priority',
  caseAssigned: 'Assigned to',
  caseCreated: 'Created on',
  caseUpdated: 'Updated on',
  
  // Case statuses
  statusOpen: 'Open',
  statusInProgress: 'In progress',
  statusClosed: 'Closed',
  statusSuspended: 'Suspended',
  
  // Case types
  homicide: 'Homicide',
  theft: 'Theft',
  fraud: 'Fraud',
  cybercrime: 'Cybercrime',
  narcotics: 'Narcotics',
  
  // Case priorities
  priorityLow: 'Low',
  priorityMedium: 'Medium',
  priorityHigh: 'High',
  priorityCritical: 'Critical',
  
  // Dashboard specific
  performanceStats: 'Performance Statistics',
  casesResolved: 'Cases Resolved',
  casesActive: 'Active Cases',
  successRate: 'Success Rate',
  averageRating: 'Average Rating',
  openWorkspace: 'Open Workspace',
  weeklyGoals: 'Weekly Goals',
  recentHistory: 'Recent History',
  lastSession: 'Last session',
  priority: 'Priority',
  
  // Desktop
  evidence: 'Evidence',
  suspects: 'Suspects',
  witnesses: 'Witnesses',
  forensics: 'Forensics',
  timeline: 'Timeline',
  notes: 'Notes',
  
  // Evidence
  evidenceNumber: 'Evidence number',
  evidenceType: 'Evidence type',
  evidenceDescription: 'Evidence description',
  evidenceLocation: 'Evidence location',
  evidenceCollected: 'Collected on',
  
  // About
  aboutTitle: 'About CaseZero Alternative',
  aboutDescription: 'Realistic police investigation system',
  
  // Footer
  currentLanguage: 'Current language',
  currentCase: 'Current case',
  
  // Departments
  homicideDept: 'Homicide',
  theftDept: 'Theft and Burglary',
  fraudDept: 'Financial Crimes',
  cybercrimeDept: 'Cybercrime',
  narcoticsDept: 'Narcotics',
  
  // Ranks
  detective: 'Detective',
  inspector: 'Inspector',
  sergeant: 'Sergeant',
  specialist: 'Specialist',
  analyst: 'Analyst',
  
  // Home page
  heroTitle: 'CaseZero Police Investigation System',
  heroSubtitle: 'Realistic Crime Investigation',
  heroDescription: 'Enter the world of criminal investigation with realistic cases, authentic evidence, and real police procedures.',
  getStarted: 'Get Started',
  learnMore: 'Learn More',
  
  // Features
  featuresTitle: 'Features',
  realisticInvestigation: 'Realistic Investigation',
  realisticInvestigationDesc: 'Cases based on real police procedures',
  evidenceAnalysis: 'Evidence Analysis',
  evidenceAnalysisDesc: 'Complete forensic analysis system',
  caseManagement: 'Case Management',
  caseManagementDesc: 'Organize and manage multiple cases simultaneously',
  
  // Feature list items
  authenticPoliceInterface: 'Authentic police computer interface',
  multipleCases: 'Multiple cases to solve',
  detectiveProgression: 'Detective progression based on performance',
  forensicAnalysis: 'Forensic analysis and clue collection',
  
  // Login page specific
  systemAccess: 'System Access',
  metropolitanPoliceDept: 'Metropolitan Police Department',
  emailOrId: 'Email or Identification Number',
  enterPassword: 'Enter your password',
  enterSystem: 'Enter System',
  authenticating: 'Authenticating...',
  noAccess: 'Don\'t have system access?',
  requestRegistration: 'Request Registration',
  testCredentials: 'Test Credentials:',
  backToHome: 'Back to home',
  
  // Register page specific
  registrationRequest: 'Registration Request',
  importantNote: 'Important:',
  systemRestricted: 'This system is restricted to authorized police department employees. All requests undergo verification before approval.',
  institutionalEmail: 'Institutional Email',
  phoneNumber: 'Phone Number',
  badgeNumberField: 'Badge Number',
  selectOption: 'Select...',
  investigationDivision: 'Investigation Division',
  criminalForensics: 'Criminal Forensics',
  cybercrimes: 'Cybercrimes',
  homicides: 'Homicides',
  frauds: 'Fraud',
  position: 'Position',
  minimumChars: 'Minimum 8 characters',
  confirmYourPassword: 'Confirm your password',
  sendingRequest: 'Sending request...',
  requestRegistrationBtn: 'Request Registration',
  alreadyHaveAccess: 'Already have system access?',
  doLogin: 'Login',
  passwordsDontMatch: 'Passwords don\'t match!',
  registrationSent: 'Registration request sent successfully! Please wait for administrator approval.',
  unexpectedError: 'Unexpected error. Please try again.',
  
  // Error handling
  networkError: 'Network error. Check your connection.',
  serverError: 'Internal server error. Please try again later.',
  notFoundError: 'Resource not found.',
  unauthorizedError: 'Access denied. Please login again.',
  forbiddenError: 'You do not have permission for this action.',
  validationError: 'Invalid data. Please check the fields.',
  timeoutError: 'Request timed out. Please try again.',
  genericError: 'Something went wrong. Please try again.',
  errorOccurred: 'An error occurred',
  tryAgain: 'Try again',
  
  // Loading states
  loadingData: 'Loading data...',
  loadingCases: 'Loading cases...',
  loadingProfile: 'Loading profile...',
  processing: 'Processing...',
  saving: 'Saving...',
  uploading: 'Uploading...',
  pleaseWait: 'Please wait...',
  almostDone: 'Almost done...',
  
  // Offline support
  offline: 'Offline',
  online: 'Online',
  offlineMessage: 'You are offline. Some features may be limited.',
  connectionRestored: 'Connection restored!',
  workingOffline: 'Working offline',
  
  // Keyboard navigation
  keyboardShortcuts: 'Keyboard shortcuts',
  pressEscToClose: 'Press Esc to close',
  pressEnterToSelect: 'Press Enter to select',
  useArrowKeys: 'Use arrow keys to navigate',
  pressTabToNavigate: 'Press Tab to navigate',
  
  // New landing page content
  digitalInterface: 'Digital Interface',
  careerProgression: 'Career Progression',
  justiceSystem: 'Justice System',
  justiceSystemDesc: 'Experience the complete investigation to conviction process',
  
  // Game mechanics
  howItWorks: 'How It Works',
  howItWorksDesc: 'Experience authentic police investigation procedures through immersive gameplay',
  investigation: 'Investigation',
  investigationDesc: 'Interview witnesses, analyze crime scenes',
  forensicsDesc: 'Process evidence in the lab',
  documentation: 'Documentation',
  documentationDesc: 'Maintain detailed case files',
  realTime: 'Real-time',
  realTimeDesc: 'Cases evolve over time',
  
  // Call to action
  readyToSolve: 'Ready to Solve Your First Case?',
  ctaDescription: 'Join the Metropolitan Police Department\'s investigation system and start your career as a detective. Access restricted to authorized personnel only.',
  accessSystem: 'Access System',
  requestAccess: 'Request Access',
  
  // New registration form translations
  newRegistrationInfo: 'For registration, you only need your first name, last name, and personal email. Your institutional email, department, position, and badge number will be automatically generated.',
  institutionalEmailPreview: '🎯 Your institutional email will be:',
  personalEmail: 'Personal Email',
  submitting: 'Submitting...',
  requestRegistrationBtn2: 'Request Registration',
  institutionalEmailInfo: 'Your institutional email will be:',
  useInstitutionalLogin: 'To login, you must use your institutional email instead of your personal email.',
  
  // Evidence visibility and forensics
  evidenceVisibility: 'Evidence Visibility',
  visibleEvidences: 'Visible Evidence',
  hiddenEvidences: 'Hidden Evidence',
  makeVisible: 'Make Visible',
  makeHidden: 'Hide',
  visibilityUpdated: 'Visibility updated successfully',
  forensicAnalysisTitle: 'Forensic Analysis',
  requestAnalysis: 'Request Analysis',
  analysisType: 'Analysis Type',
  analysisInProgress: 'Analysis in Progress',
  analysisCompleted: 'Analysis Completed',
  analysisResults: 'Analysis Results',
  noAnalysisAvailable: 'No analysis available for this evidence',
  timeBasedAnalysis: 'Time-based analysis',
  estimatedCompletion: 'Estimated completion',
  analysisTypeNotSupported: 'Analysis type not supported for this evidence',
  selectAnalysisType: 'Select analysis type',
  supportedAnalyses: 'Supported analyses',
  
  // Analysis types
  dnaAnalysis: 'DNA Analysis',
  fingerprintAnalysis: 'Fingerprint Analysis',
  digitalForensics: 'Digital Forensics',
  ballisticsAnalysis: 'Ballistics Analysis',
  toxicologyAnalysis: 'Toxicology Analysis',
  handwritingAnalysis: 'Handwriting Analysis',
  voiceAnalysis: 'Voice Analysis',
  traceAnalysis: 'Trace Analysis',
  generalAnalysis: 'General Analysis',
  
  // Case processing
  caseProcessing: 'Case Processing',
  processingAllCases: 'Processing all cases',
  processingCase: 'Processing case',
  caseProcessed: 'Case processed successfully',
  caseAlreadyProcessed: 'Case has already been processed',
  processingStatus: 'Processing status',
  
  // Rank-based access
  accessDenied: 'Access denied',
  insufficientRank: 'Insufficient rank',
  rankRequired: 'Required rank',
  yourRank: 'Your rank',
  availableForYourRank: 'Available for your rank',
  
  // Generate Case
  generateCase: 'Generate Case',
  generateNewCase: 'Generate New Case',
  caseGeneration: 'Case Generation',
  caseLocation: 'Case Location',
  incidentDateTime: 'Incident Date & Time',
  casePitch: 'Case Description',
  caseTwist: 'Plot Twists',
  caseDifficulty: 'Difficulty',
  targetDuration: 'Target Duration (minutes)',
  constraints: 'Constraints',
  timezone: 'Timezone',
  generateImages: 'Generate Images',
  generateCaseBtn: 'Generate Case',
  generatingCase: 'Generating case...',
  caseGeneratedSuccess: 'Case generated successfully!',
  caseGenerationError: 'Error generating case',
  fillRequiredFields: 'Please fill all required fields',
  easy: 'Easy',
  medium: 'Medium',
  hard: 'Hard',
  caseGenerationForm: 'Case Generation Form',
  caseGenerationDesc: 'Select the difficulty level and we\'ll generate a complete investigation case for you.',
  caseGenerationSimpleDesc: 'Just choose a difficulty level and we\'ll create an engaging case with all the details.',
  backToDashboard: 'Back to Dashboard',
  selectDifficulty: 'Select Case Difficulty',
  difficultyHelpText: 'Choose the complexity level for your investigation case',
};