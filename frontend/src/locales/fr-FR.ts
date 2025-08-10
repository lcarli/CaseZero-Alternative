import type { Translations } from '../types/i18n';

export const frFR: Translations = {
  // Common
  loading: 'Chargement...',
  error: 'Erreur',
  success: 'Succès',
  cancel: 'Annuler',
  save: 'Enregistrer',
  delete: 'Supprimer',
  edit: 'Modifier',
  close: 'Fermer',
  back: 'Retour',
  next: 'Suivant',
  previous: 'Précédent',
  
  // Navigation
  home: 'Accueil',
  dashboard: 'Tableau de bord',
  login: 'Connexion',
  register: 'S\'inscrire',
  logout: 'Déconnexion',
  
  // Authentication
  username: 'Nom d\'utilisateur',
  email: 'E-mail',
  password: 'Mot de passe',
  confirmPassword: 'Confirmer le mot de passe',
  firstName: 'Prénom',
  lastName: 'Nom de famille',
  department: 'Département',
  badgeNumber: 'Numéro de badge',
  rank: 'Grade',
  specialization: 'Spécialisation',
  
  // Authentication messages
  loginSuccess: 'Connexion réussie',
  loginError: 'Erreur de connexion',
  registerSuccess: 'Inscription réussie',
  registerError: 'Erreur d\'inscription',
  logoutSuccess: 'Déconnexion réussie',
  
  // Dashboard
  welcomeBack: 'Bon retour',
  availableCases: 'Affaires disponibles',
  recentActivity: 'Activité récente',
  caseProgress: 'Progression de l\'affaire',
  profile: 'Profil',
  settings: 'Paramètres',
  language: 'Langue',
  
  // Cases
  caseNumber: 'Numéro d\'affaire',
  caseTitle: 'Titre de l\'affaire',
  caseDescription: 'Description de l\'affaire',
  caseStatus: 'Statut de l\'affaire',
  caseType: 'Type d\'affaire',
  casePriority: 'Priorité de l\'affaire',
  caseAssigned: 'Assigné à',
  caseCreated: 'Créé le',
  caseUpdated: 'Mis à jour le',
  
  // Case statuses
  statusOpen: 'Ouvert',
  statusInProgress: 'En cours',
  statusClosed: 'Fermé',
  statusSuspended: 'Suspendu',
  
  // Case types
  homicide: 'Homicide',
  theft: 'Vol',
  fraud: 'Fraude',
  cybercrime: 'Cybercriminalité',
  narcotics: 'Stupéfiants',
  
  // Case priorities
  priorityLow: 'Faible',
  priorityMedium: 'Moyenne',
  priorityHigh: 'Élevée',
  priorityCritical: 'Critique',
  
  // Dashboard specific
  performanceStats: 'Statistiques de Performance',
  casesResolved: 'Affaires Résolues',
  casesActive: 'Affaires Actives',
  successRate: 'Taux de Réussite',
  averageRating: 'Note Moyenne',
  openWorkspace: 'Ouvrir l\'Espace de Travail',
  weeklyGoals: 'Objectifs Hebdomadaires',
  recentHistory: 'Historique Récent',
  lastSession: 'Dernière session',
  priority: 'Priorité',
  
  // Desktop
  evidence: 'Preuves',
  suspects: 'Suspects',
  witnesses: 'Témoins',
  forensics: 'Médecine légale',
  timeline: 'Chronologie',
  notes: 'Notes',
  
  // Evidence
  evidenceNumber: 'Numéro de preuve',
  evidenceType: 'Type de preuve',
  evidenceDescription: 'Description de la preuve',
  evidenceLocation: 'Lieu de la preuve',
  evidenceCollected: 'Collectée le',
  
  // About
  aboutTitle: 'À propos de CaseZero Alternative',
  aboutDescription: 'Système d\'investigation policière réaliste',
  
  // Footer
  currentLanguage: 'Langue actuelle',
  currentCase: 'Affaire actuelle',
  
  // Departments
  homicideDept: 'Homicides',
  theftDept: 'Vols et Cambriolages',
  fraudDept: 'Crimes Financiers',
  cybercrimeDept: 'Cybercriminalité',
  narcoticsDept: 'Stupéfiants',
  
  // Ranks
  detective: 'Détective',
  inspector: 'Inspecteur',
  sergeant: 'Sergent',
  specialist: 'Spécialiste',
  analyst: 'Analyste',
  
  // Home page
  heroTitle: 'Système d\'Investigation Policière CaseZero',
  heroSubtitle: 'Investigation Criminelle Réaliste',
  heroDescription: 'Entrez dans le monde de l\'investigation criminelle avec des affaires réalistes, des preuves authentiques et de vraies procédures policières.',
  getStarted: 'Commencer',
  learnMore: 'En savoir plus',
  
  // Features
  featuresTitle: 'Fonctionnalités',
  realisticInvestigation: 'Investigation Réaliste',
  realisticInvestigationDesc: 'Affaires basées sur de vraies procédures policières',
  evidenceAnalysis: 'Analyse de Preuves',
  evidenceAnalysisDesc: 'Système complet d\'analyse forensique',
  caseManagement: 'Gestion d\'Affaires',
  caseManagementDesc: 'Organisez et gérez plusieurs affaires simultanément',
  
  // Feature list items
  authenticPoliceInterface: 'Interface informatique policière authentique',
  multipleCases: 'Plusieurs affaires à résoudre',
  detectiveProgression: 'Progression du détective basée sur la performance',
  forensicAnalysis: 'Analyse forensique et collecte d\'indices',
  
  // Login page specific
  systemAccess: 'Accès au Système',
  metropolitanPoliceDept: 'Metropolitan Police Department',
  emailOrId: 'Email ou Numéro d\'Identification',
  enterPassword: 'Entrez votre mot de passe',
  enterSystem: 'Entrer dans le Système',
  authenticating: 'Authentification...',
  noAccess: 'Vous n\'avez pas accès au système?',
  requestRegistration: 'Demander l\'Inscription',
  testCredentials: 'Identifiants de Test:',
  backToHome: 'Retour à l\'accueil',
  
  // Register page specific
  registrationRequest: 'Demande d\'Inscription',
  importantNote: 'Important:',
  systemRestricted: 'Ce système est réservé aux employés autorisés du département de police. Toutes les demandes sont vérifiées avant approbation.',
  institutionalEmail: 'Email Institutionnel',
  phoneNumber: 'Numéro de Téléphone',
  badgeNumberField: 'Numéro de Badge',
  selectOption: 'Sélectionner...',
  investigationDivision: 'Division d\'Investigation',
  criminalForensics: 'Criminalistique',
  cybercrimes: 'Cybercrimes',
  homicides: 'Homicides',
  frauds: 'Fraudes',
  position: 'Poste',
  minimumChars: 'Minimum 8 caractères',
  confirmYourPassword: 'Confirmez votre mot de passe',
  sendingRequest: 'Envoi de la demande...',
  requestRegistrationBtn: 'Demander l\'Inscription',
  alreadyHaveAccess: 'Vous avez déjà accès au système?',
  doLogin: 'Se Connecter',
  passwordsDontMatch: 'Les mots de passe ne correspondent pas!',
  registrationSent: 'Demande d\'inscription envoyée avec succès! Veuillez attendre l\'approbation de l\'administrateur.',
  unexpectedError: 'Erreur inattendue. Veuillez réessayer.',
  
  // New landing page content
  digitalInterface: 'Interface Numérique',
  careerProgression: 'Progression de Carrière',
  justiceSystem: 'Système de Justice',
  justiceSystemDesc: 'Expérimentez le processus complet d\'investigation jusqu\'à la condamnation',
  
  // Game mechanics
  howItWorks: 'Comment Ça Marche',
  howItWorksDesc: 'Expérimentez des procédures d\'investigation policière authentiques grâce à un gameplay immersif',
  investigation: 'Investigation',
  investigationDesc: 'Interrogez les témoins, analysez les scènes de crime',
  forensicsDesc: 'Traitez les preuves dans le laboratoire',
  documentation: 'Documentation',
  documentationDesc: 'Maintenez des dossiers d\'affaires détaillés',
  realTime: 'Temps Réel',
  realTimeDesc: 'Les affaires évoluent au fil du temps',
  
  // Call to action
  readyToSolve: 'Prêt à Résoudre Votre Première Affaire?',
  ctaDescription: 'Rejoignez le système d\'investigation du Département de Police Métropolitaine et commencez votre carrière en tant que détective. Accès restreint au personnel autorisé uniquement.',
  accessSystem: 'Accéder au Système',
  requestAccess: 'Demander l\'Accès',
  
  // New registration form translations
  newRegistrationInfo: 'Pour l\'inscription, vous n\'avez besoin que de votre prénom, nom de famille et email personnel. Votre email institutionnel, département, poste et numéro de badge seront générés automatiquement.',
  institutionalEmailPreview: '🎯 Votre email institutionnel sera:',
  personalEmail: 'Email Personnel',
  submitting: 'Envoi en cours...',
  requestRegistrationBtn2: 'Demander l\'Inscription',
  institutionalEmailInfo: 'Votre email institutionnel sera:',
  useInstitutionalLogin: 'Pour vous connecter, vous devez utiliser votre email institutionnel au lieu de votre email personnel.',
  
  // Evidence visibility and forensics
  evidenceVisibility: 'Visibilité des Preuves',
  visibleEvidences: 'Preuves Visibles',
  hiddenEvidences: 'Preuves Cachées',
  makeVisible: 'Rendre Visible',
  makeHidden: 'Masquer',
  visibilityUpdated: 'Visibilité mise à jour avec succès',
  forensicAnalysisTitle: 'Analyse Légale',
  requestAnalysis: 'Demander une Analyse',
  analysisType: 'Type d\'Analyse',
  analysisInProgress: 'Analyse en Cours',
  analysisCompleted: 'Analyse Terminée',
  analysisResults: 'Résultats d\'Analyse',
  noAnalysisAvailable: 'Aucune analyse disponible pour cette preuve',
  timeBasedAnalysis: 'Analyse basée sur le temps',
  estimatedCompletion: 'Achèvement estimé',
  analysisTypeNotSupported: 'Type d\'analyse non pris en charge pour cette preuve',
  selectAnalysisType: 'Sélectionner le type d\'analyse',
  supportedAnalyses: 'Analyses prises en charge',
  
  // Analysis types
  dnaAnalysis: 'Analyse ADN',
  fingerprintAnalysis: 'Analyse d\'Empreintes Digitales',
  digitalForensics: 'Informatique Légale',
  ballisticsAnalysis: 'Analyse Balistique',
  toxicologyAnalysis: 'Analyse Toxicologique',
  handwritingAnalysis: 'Analyse d\'Écriture',
  voiceAnalysis: 'Analyse Vocale',
  traceAnalysis: 'Analyse de Traces',
  generalAnalysis: 'Analyse Générale',
  
  // Case processing
  caseProcessing: 'Traitement des Affaires',
  processingAllCases: 'Traitement de toutes les affaires',
  processingCase: 'Traitement de l\'affaire',
  caseProcessed: 'Affaire traitée avec succès',
  caseAlreadyProcessed: 'L\'affaire a déjà été traitée',
  processingStatus: 'Statut du traitement',
  
  // Rank-based access
  accessDenied: 'Accès refusé',
  insufficientRank: 'Rang insuffisant',
  rankRequired: 'Rang requis',
  yourRank: 'Votre rang',
  availableForYourRank: 'Disponible pour votre rang',
  
  // Generate Case
  generateCase: 'Générer une Affaire',
  generateNewCase: 'Générer une Nouvelle Affaire',
  caseGeneration: 'Génération d\'Affaires',
  caseLocation: 'Lieu de l\'Affaire',
  incidentDateTime: 'Date et Heure de l\'Incident',
  casePitch: 'Description de l\'Affaire',
  caseTwist: 'Rebondissements',
  caseDifficulty: 'Difficulté',
  targetDuration: 'Durée Cible (minutes)',
  constraints: 'Contraintes',
  timezone: 'Fuseau Horaire',
  generateImages: 'Générer des Images',
  generateCaseBtn: 'Générer l\'Affaire',
  generatingCase: 'Génération de l\'affaire...',
  caseGeneratedSuccess: 'Affaire générée avec succès !',
  caseGenerationError: 'Erreur lors de la génération de l\'affaire',
  fillRequiredFields: 'Veuillez remplir tous les champs obligatoires',
  easy: 'Facile',
  medium: 'Moyen',
  hard: 'Difficile',
  caseGenerationForm: 'Formulaire de Génération d\'Affaires',
  caseGenerationDesc: 'Remplissez les détails ci-dessous pour générer une nouvelle affaire d\'enquête en utilisant l\'IA.',
  backToDashboard: 'Retour au Tableau de Bord',
};