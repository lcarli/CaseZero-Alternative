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
  casesByRank: string;
  lastSession: string;
  priority: string;
  divisionHeatmap: string;
  evidenceProgress: string;
  commandBulletins: string;
  noRecentActivity: string;
  noDataAvailable: string;
  viewDossier: string;
  resolvedBadge: string;
  activityResolved: string;
  activityResolvedUngraded: string;
  activityAttempted: string;
  activityAttemptedUngraded: string;
  filterByDifficulty: string;
  allDifficulties: string;
  hideResolvedCases: string;
  noCasesMatchFilters: string;
  // Dashboard widgets
  casesByDifficulty: string;
  promotionProgress: string;
  currentRank: string;
  nextRank: string;
  casesToNextRank: string;
  maxRankReached: string;
  viewProfile: string;
  // Rank display names (aligned with backend DetectiveRank)
  rankRook: string;
  rankDetective: string;
  rankDetective2: string;
  rankSergeant: string;
  rankLieutenant: string;
  rankCaptain: string;
  rankCommander: string;
  // Promotion notification (submit banner + in-game e-mail)
  promotionToastTitle: string;
  promotionToastBody: string;
  promotionEmailSubject: string;
  promotionEmailBody: string;
  // Global inbox
  inboxTitle: string;
  inboxEmpty: string;
  inboxMarkRead: string;
  inboxUnread: string;
  // Profile page
  profileTitle: string;
  profileBackToDashboard: string;
  profileAgentSection: string;
  profileLastPromotion: string;
  profileNeverPromoted: string;
  profileCaseHistory: string;
  profileColCase: string;
  profileColDifficulty: string;
  profileColAttempts: string;
  profileColBestScore: string;
  profileColOutcome: string;
  profileColLastAttempt: string;
  profileOutcomeResolved: string;
  profileOutcomeResolvedUngraded: string;
  profileOutcomeNotResolved: string;
  profileScoreByDifficulty: string;
  profileScoreColPlayed: string;
  profileScoreColResolved: string;
  profileScoreColAvg: string;
  profileSubmissionsTimeline: string;
  profileRankHistory: string;
  profileRankInitial: string;
  profileCategoryBreakdown: string;
  profileCategoryCulprit: string;
  profileCategoryEvidence: string;
  profileCategoryAnalysis: string;
  profileCategoryQuestions: string;
  profileCategoryNoData: string;
  profileCategorySampleSize: string;
  
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
  loadingCaseSession: string;
  gameClockSyncFailed: string;
  gameClockServerOwned: string;
  gameClockTitle: string;
  gameClockActive: string;
  gameClockPaused: string;
  gameClockStarted: string;
  gameClockElapsed: string;
  gameClockSpeed: string;
  gameClockMultiplier: string;
  gameClockPause: string;
  gameClockResume: string;
  forensicPending: string;
  forensicCancelled: string;
  forensicFailed: string;
  forensicLoadFailed: string;
  forensicRequestFailed: string;
  forensicResultUnavailable: string;
  forensicTimeRemaining: string;
  forensicAwaitingLab: string;
  forensicConfigurationFailed: string;
  forensicOpenReport: string;
  forensicOpenEmail: string;
  forensicNoResultMaterials: string;
  forensicAnalysisId: string;
  forensicClockPolicy: string;
  forensicSelectCase: string;
  forensicLoading: string;
  forensicNoPending: string;
  forensicNoCompleted: string;
  forensicSelectEvidence: string;
  forensicChooseEvidence: string;
  forensicNoCompatibleEvidence: string;
  forensicDuration: string;
  forensicSubmitting: string;
  forensicCloseEmail: string;
  submitCaseAnalysisPlaceholder: string;
  
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
  generationLanguage: string;
  generationTitleOptional: string;
  generationLocationOptional: string;
  generationThemeOptional: string;
  generationCaseIdOptional: string;
  generationSeedOptional: string;
  generationStarting: string;
  generationJob: string;
  generationQueued: string;
  generationSkipped: string;
  generationAttempt: string;
  generationAttemptHistory: string;
  generationRetryScheduled: string;
  generationSolverScore: string;
  generationUnknownError: string;
  generationPollError: string;
  generationStartError: string;
  generationAnother: string;
  generationValidationErrors: string;
  generationGraphValidation: string;
  generationSolutionWitness: string;
  generationRepairOperations: string;
  generationFirstPass: string;
  generationAdvisoryFindings: string;
  generationPassed: string;
  generationNotPassed: string;
  generationYes: string;
  generationNo: string;
  stageCaseDesign: string;
  stageGraphConstruction: string;
  stageEvidenceProduction: string;
  stageForensicWorkflow: string;
  stageSolutionDesign: string;
  stageDeterministicValidation: string;
  stageSolutionWitness: string;
  stageAdvisoryReview: string;
  stageTargetedRepair: string;
  stageFinalValidation: string;
  stageFinalization: string;
  phaseCaseBible: string;
  phasePlotOutline: string;
  phaseSuspectCards: string;
  generationTitle: string;
  generationOptional: string;
  generationTitlePlaceholder: string;
  generationLocationPlaceholder: string;
  generationTheme: string;
  generationThemePlaceholder: string;
  generationCaseId: string;
  generationAuto: string;
  generationSeed: string;
  generationRandom: string;
  generationSaveLocally: string;
  generationPreviewHelp: string;
  generationNotRun: string;
  generationUnknown: string;
  generationApproved: string;
  generationPersisted: string;
  generationPublished: string;
  generationOutputMode: string;
  generationPreview: string;
  generationPublishedMode: string;
  generationLocalOnly: string;
  generationPdfsRendered: string;
  generationImagesRendered: string;
  generationAssetsSkipped: string;
  generationBlobsPublished: string;
  generationAutoFixes: string;
  generationRefineAttempted: string;
  generationRevision: string;
  generationReviewedRevision: string;
  generationBundleId: string;
  generationBundleVerified: string;
  generationBundleRevision: string;
  generationBundleAssets: string;
  generationBundleBytes: string;
  generationBundleChecksum: string;
  generationRepairAccepted: string;
  generationRepairChanges: string;
  generationRepairOriginalIssues: string;
  generationRedTeamVerdict: string;
  generationReviewOk: string;
  generationReviewNeeded: string;
  generationReviewRejected: string;
  generationSolverDiagnostic: string;
  generationQualityScope: string;
  generationPlayerInvestigationScope: string;
  generationPlayerQualityScope: string;
  generationQualityIssues: string;
  generationQualityIssue: string;
  generationBlocking: string;
  generationWarning: string;
  generationTechnicalDetails: string;
  generationSeverityLow: string;
  generationSeverityMedium: string;
  generationSeverityHigh: string;
  generationIssueSchema: string;
  generationIssueConsistency: string;
  generationIssueReviewUnavailable: string;
  generationIssueReviewInvalid: string;
  generationIssueReviewFinding: string;
  generationIssueRefine: string;
  generationIssueRepair: string;
  generationIssueReviewRevision: string;
  generationIssueRender: string;
  generationIssueBundle: string;
  generationIssueIntegrity: string;
  generationIssuePublicationConflict: string;
  generationPhaseVerifyBundle: string;
  generationPhasePreparePublication: string;
  generationIssueSkipped: string;
  generationIssueMissingFile: string;
  generationIssuePublish: string;
  generationIssuePersist: string;
  generationPhasePlot: string;
  generationPhaseCaseBible: string;
  generationPhaseSuspects: string;
  generationPhaseAssetPlan: string;
  generationPhaseAssets: string;
  generationPhaseForensicsPlan: string;
  generationPhaseOutcomes: string;
  generationPhaseMechanicalRules: string;
  generationPhaseSolution: string;
  generationPhaseQuestions: string;
  generationPhaseConsistency: string;
  generationPhaseAutoFix: string;
  generationPhaseReview: string;
  generationPhasePrepareMaterials: string;
  generationPhasePlayerInvestigation: string;
  generationPhaseBlockingReview: string;
  generationPhaseRefine: string;
  generationPhaseRepairReview: string;
  generationPhaseQualityGate: string;
  generationPhaseRender: string;
  generationPhasePublish: string;
  generationPhasePersist: string;
  generationRankRookie: string;
  generationRankDetective2: string;
  generationRankLieutenant: string;
  generationRankCaptain: string;
  generationRankCommander: string;
  generationLanguageEnglish: string;
  generationLanguagePortuguese: string;
  generationLanguageSpanish: string;
  generationLanguageFrench: string;

  // Case email and attachment discovery
  emailInbox: string;
  emailMessages: string;
  emailEmpty: string;
  emailLoading: string;
  emailFrom: string;
  emailTo: string;
  emailDate: string;
  emailAttachments: string;
  emailAttachmentFallback: string;
  emailAttachmentId: string;
  emailAlreadyInFiles: string;
  emailDownloadToFiles: string;
  emailDownloading: string;
  emailInFiles: string;
  emailDownload: string;
  emailSelect: string;
  emailOpenError: string;
  emailDownloadError: string;
  
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

  // Submit Case (v2)
  submitCaseTitle: string;
  submitCaseSuspectLabel: string;
  submitCaseEvidenceLabel: string;
  submitCaseAnalysisLabel: string;
  submitCaseAnalysisLoading: string;
  submitCaseAnalysisEmpty: string;
  submitCaseQuestionsHeader: string;
  submitCaseSubmitButton: string;
  submitCaseAttemptsRemaining: string;
  submitCaseAttemptsExhausted: string;
  submitCaseResultCorrect: string;
  submitCaseResultIncorrect: string;
  submitCaseScore: string;
  submitCaseBreakdownCulprit: string;
  submitCaseBreakdownEvidence: string;
  submitCaseBreakdownAnalysis: string;
  submitCaseBreakdownQuestions: string;
  submitCaseFeedbackCorrect: string;
  submitCaseFeedbackCorrectUngraded: string;
  submitCaseFeedbackIncorrectRetry: string;
  submitCaseFeedbackIncorrectLastGraded: string;
  submitCaseFeedbackIncorrectUngraded: string;
  submitCaseUngradedBanner: string;
  submitCaseExplanationHeader: string;
  submitCaseSuspectsEmpty: string;
  submitCaseProgressTitle: string;
  submitCaseProgressSuspect: string;
  submitCaseProgressEvidence: string;
  submitCaseProgressAnalysis: string;
  submitCaseProgressQuestions: string;
  submitCaseProgressReady: string;
  submitCaseProgressMissingSuspect: string;
  submitCaseProgressMissingQuestions: string;

  // Dashboard V2
  dashboardRequiredRank: string;
  dashboardDifficulty: string;
  dashboardEmpty: string;

  // Notifications
  notificationsEntityRevealedEmail: string;
  notificationsEntityRevealedAsset: string;
  notificationsEntityRevealedSuspect: string;
}

export const SUPPORTED_LANGUAGES: Language[] = [
  { code: 'pt-BR', name: 'Português (Brasil)', flag: '🇧🇷' },
  { code: 'en-US', name: 'English (United States)', flag: '🇺🇸' },
  { code: 'fr-FR', name: 'Français (France)', flag: '🇫🇷' },
  { code: 'es-ES', name: 'Español (España)', flag: '🇪🇸' },
];

export const DEFAULT_LANGUAGE = 'pt-BR';