import type { Translations } from '../types/i18n';

export const esES: Translations = {
  // Common
  loading: 'Cargando...',
  error: 'Error',
  success: 'Éxito',
  cancel: 'Cancelar',
  save: 'Guardar',
  delete: 'Eliminar',
  edit: 'Editar',
  close: 'Cerrar',
  back: 'Atrás',
  next: 'Siguiente',
  previous: 'Anterior',
  
  // Navigation
  home: 'Inicio',
  dashboard: 'Panel',
  login: 'Iniciar sesión',
  register: 'Registrarse',
  logout: 'Cerrar sesión',
  
  // Authentication
  username: 'Nombre de usuario',
  email: 'Correo electrónico',
  password: 'Contraseña',
  confirmPassword: 'Confirmar contraseña',
  firstName: 'Nombre',
  lastName: 'Apellidos',
  department: 'Departamento',
  badgeNumber: 'Número de placa',
  rank: 'Rango',
  specialization: 'Especialización',
  
  // Authentication messages
  loginSuccess: 'Inicio de sesión exitoso',
  loginError: 'Error de inicio de sesión',
  registerSuccess: 'Registro exitoso',
  registerError: 'Error de registro',
  logoutSuccess: 'Cierre de sesión exitoso',
  
  // Dashboard
  welcomeBack: 'Bienvenido de vuelta',
  availableCases: 'Casos disponibles',
  recentActivity: 'Actividad reciente',
  caseProgress: 'Progreso del caso',
  profile: 'Perfil',
  settings: 'Configuración',
  language: 'Idioma',
  
  // Cases
  caseNumber: 'Número de caso',
  caseTitle: 'Título del caso',
  caseDescription: 'Descripción del caso',
  caseStatus: 'Estado del caso',
  caseType: 'Tipo de caso',
  casePriority: 'Prioridad del caso',
  caseAssigned: 'Asignado a',
  caseCreated: 'Creado el',
  caseUpdated: 'Actualizado el',
  
  // Case statuses
  statusOpen: 'Abierto',
  statusInProgress: 'En progreso',
  statusClosed: 'Cerrado',
  statusSuspended: 'Suspendido',
  
  // Case types
  homicide: 'Homicidio',
  theft: 'Robo',
  fraud: 'Fraude',
  cybercrime: 'Cibercrimen',
  narcotics: 'Narcóticos',
  
  // Case priorities
  priorityLow: 'Baja',
  priorityMedium: 'Media',
  priorityHigh: 'Alta',
  priorityCritical: 'Crítica',
  
  // Dashboard specific
  performanceStats: 'Estadísticas de Rendimiento',
  casesResolved: 'Casos Resueltos',
  casesActive: 'Casos Activos',
  successRate: 'Tasa de Éxito',
  averageRating: 'Calificación Promedio',
  openWorkspace: 'Abrir Espacio de Trabajo',
  weeklyGoals: 'Objetivos Semanales',
  recentHistory: 'Historial Reciente',
  lastSession: 'Última sesión',
  priority: 'Prioridad',
  
  // Desktop
  evidence: 'Evidencias',
  suspects: 'Sospechosos',
  witnesses: 'Testigos',
  forensics: 'Forense',
  timeline: 'Línea de tiempo',
  notes: 'Notas',
  
  // Evidence
  evidenceNumber: 'Número de evidencia',
  evidenceType: 'Tipo de evidencia',
  evidenceDescription: 'Descripción de evidencia',
  evidenceLocation: 'Ubicación de evidencia',
  evidenceCollected: 'Recopilada el',
  
  // About
  aboutTitle: 'Acerca de CaseZero Alternative',
  aboutDescription: 'Sistema de investigación policial realista',
  
  // Footer
  currentLanguage: 'Idioma actual',
  currentCase: 'Caso actual',
  
  // Departments
  homicideDept: 'Homicidios',
  theftDept: 'Robos y Hurtos',
  fraudDept: 'Crímenes Financieros',
  cybercrimeDept: 'Cibercrimen',
  narcoticsDept: 'Narcóticos',
  
  // Ranks
  detective: 'Detective',
  inspector: 'Inspector',
  sergeant: 'Sargento',
  specialist: 'Especialista',
  analyst: 'Analista',
  
  // Home page
  heroTitle: 'Sistema de Investigación Policial CaseZero',
  heroSubtitle: 'Investigación Criminal Realista',
  heroDescription: 'Entra en el mundo de la investigación criminal con casos realistas, evidencias auténticas y procedimientos policiales reales.',
  getStarted: 'Comenzar',
  learnMore: 'Saber más',
  
  // Features
  featuresTitle: 'Funcionalidades',
  realisticInvestigation: 'Investigación Realista',
  realisticInvestigationDesc: 'Casos basados en procedimientos policiales reales',
  evidenceAnalysis: 'Análisis de Evidencias',
  evidenceAnalysisDesc: 'Sistema completo de análisis forense',
  caseManagement: 'Gestión de Casos',
  caseManagementDesc: 'Organiza y gestiona múltiples casos simultáneamente',
  
  // Feature list items
  authenticPoliceInterface: 'Interfaz de computadora policial auténtica',
  multipleCases: 'Múltiples casos para resolver',
  detectiveProgression: 'Progresión del detective basada en rendimiento',
  forensicAnalysis: 'Análisis forense y recolección de pistas',
  
  // Login page specific
  systemAccess: 'Acceso al Sistema',
  metropolitanPoliceDept: 'Metropolitan Police Department',
  emailOrId: 'Email o Número de Identificación',
  enterPassword: 'Ingrese su contraseña',
  enterSystem: 'Ingresar al Sistema',
  authenticating: 'Autenticando...',
  noAccess: '¿No tiene acceso al sistema?',
  requestRegistration: 'Solicitar Registro',
  testCredentials: 'Credenciales de Prueba:',
  backToHome: 'Volver al inicio',
  
  // Register page specific
  registrationRequest: 'Solicitud de Registro',
  importantNote: 'Importante:',
  systemRestricted: 'Este sistema está restringido a empleados autorizados del departamento de policía. Todas las solicitudes pasan por verificación antes de la aprobación.',
  institutionalEmail: 'Email Institucional',
  phoneNumber: 'Número de Teléfono',
  badgeNumberField: 'Número de Placa',
  selectOption: 'Seleccionar...',
  investigationDivision: 'División de Investigación',
  criminalForensics: 'Criminalística',
  cybercrimes: 'Cibercrímenes',
  homicides: 'Homicidios',
  frauds: 'Fraudes',
  position: 'Cargo',
  minimumChars: 'Mínimo 8 caracteres',
  confirmYourPassword: 'Confirme su contraseña',
  sendingRequest: 'Enviando solicitud...',
  requestRegistrationBtn: 'Solicitar Registro',
  alreadyHaveAccess: '¿Ya tiene acceso al sistema?',
  doLogin: 'Iniciar Sesión',
  passwordsDontMatch: '¡Las contraseñas no coinciden!',
  registrationSent: '¡Solicitud de registro enviada exitosamente! Espere la aprobación del administrador.',
  unexpectedError: 'Error inesperado. Inténtelo de nuevo.',
  
  // Error handling
  networkError: 'Error de red. Verifique su conexión.',
  serverError: 'Error interno del servidor. Inténtelo más tarde.',
  notFoundError: 'Recurso no encontrado.',
  unauthorizedError: 'Acceso denegado. Inicie sesión nuevamente.',
  forbiddenError: 'No tiene permisos para esta acción.',
  validationError: 'Datos inválidos. Verifique los campos.',
  timeoutError: 'Tiempo de espera agotado. Inténtelo de nuevo.',
  genericError: 'Algo salió mal. Inténtelo de nuevo.',
  errorOccurred: 'Ocurrió un error',
  tryAgain: 'Intentar de nuevo',
  
  // Loading states
  loadingData: 'Cargando datos...',
  loadingCases: 'Cargando casos...',
  loadingProfile: 'Cargando perfil...',
  processing: 'Procesando...',
  saving: 'Guardando...',
  uploading: 'Subiendo...',
  pleaseWait: 'Por favor espere...',
  almostDone: 'Casi terminado...',
  
  // Offline support
  offline: 'Sin conexión',
  online: 'Conectado',
  offlineMessage: 'Está sin conexión. Algunas funciones pueden estar limitadas.',
  connectionRestored: '¡Conexión restaurada!',
  workingOffline: 'Trabajando sin conexión',
  
  // Keyboard navigation
  keyboardShortcuts: 'Atajos de teclado',
  pressEscToClose: 'Presione Esc para cerrar',
  pressEnterToSelect: 'Presione Enter para seleccionar',
  useArrowKeys: 'Use las flechas para navegar',
  pressTabToNavigate: 'Presione Tab para navegar',
  
  // New landing page content
  digitalInterface: 'Interfaz Digital',
  careerProgression: 'Progresión de Carrera',
  justiceSystem: 'Sistema de Justicia',
  justiceSystemDesc: 'Experimenta el proceso completo de investigación hasta condena',
  
  // Game mechanics
  howItWorks: 'Cómo Funciona',
  howItWorksDesc: 'Experimenta procedimientos auténticos de investigación policial a través de jugabilidad inmersiva',
  investigation: 'Investigación',
  investigationDesc: 'Entrevista testigos, analiza escenas del crimen',
  forensicsDesc: 'Procesa evidencias en el laboratorio',
  documentation: 'Documentación',
  documentationDesc: 'Mantén archivos detallados de casos',
  realTime: 'Tiempo Real',
  realTimeDesc: 'Los casos evolucionan con el tiempo',
  
  // Call to action
  readyToSolve: '¿Listo para Resolver Tu Primer Caso?',
  ctaDescription: 'Únete al sistema de investigación del Departamento de Policía Metropolitana y comienza tu carrera como detective. Acceso restringido solo a personal autorizado.',
  accessSystem: 'Acceder al Sistema',
  requestAccess: 'Solicitar Acceso',
  
  // New registration form translations
  newRegistrationInfo: 'Para el registro solo necesitas tu nombre, apellido y email personal. Tu email institucional, departamento, posición y número de placa serán generados automáticamente.',
  institutionalEmailPreview: '🎯 Tu email institucional será:',
  personalEmail: 'Email Personal',
  submitting: 'Enviando...',
  requestRegistrationBtn2: 'Solicitar Registro',
  institutionalEmailInfo: 'Tu email institucional será:',
  useInstitutionalLogin: 'Para iniciar sesión, debes usar tu email institucional en lugar de tu email personal.',
  
  // Evidence visibility and forensics
  evidenceVisibility: 'Visibilidad de Evidencia',
  visibleEvidences: 'Evidencias Visibles',
  hiddenEvidences: 'Evidencias Ocultas',
  makeVisible: 'Hacer Visible',
  makeHidden: 'Ocultar',
  visibilityUpdated: 'Visibilidad actualizada con éxito',
  forensicAnalysisTitle: 'Análisis Forense',
  requestAnalysis: 'Solicitar Análisis',
  analysisType: 'Tipo de Análisis',
  analysisInProgress: 'Análisis en Progreso',
  analysisCompleted: 'Análisis Completado',
  analysisResults: 'Resultados del Análisis',
  noAnalysisAvailable: 'No hay análisis disponible para esta evidencia',
  timeBasedAnalysis: 'Análisis basado en tiempo',
  estimatedCompletion: 'Finalización estimada',
  analysisTypeNotSupported: 'Tipo de análisis no soportado para esta evidencia',
  selectAnalysisType: 'Seleccionar tipo de análisis',
  supportedAnalyses: 'Análisis soportados',
  
  // Analysis types
  dnaAnalysis: 'Análisis de ADN',
  fingerprintAnalysis: 'Análisis de Huellas Dactilares',
  digitalForensics: 'Forense Digital',
  ballisticsAnalysis: 'Análisis Balístico',
  toxicologyAnalysis: 'Análisis Toxicológico',
  handwritingAnalysis: 'Análisis de Escritura',
  voiceAnalysis: 'Análisis de Voz',
  traceAnalysis: 'Análisis de Rastros',
  generalAnalysis: 'Análisis General',
  
  // Case processing
  caseProcessing: 'Procesamiento de Casos',
  processingAllCases: 'Procesando todos los casos',
  processingCase: 'Procesando caso',
  caseProcessed: 'Caso procesado exitosamente',
  caseAlreadyProcessed: 'El caso ya ha sido procesado',
  processingStatus: 'Estado del procesamiento',
  
  // Rank-based access
  accessDenied: 'Acceso denegado',
  insufficientRank: 'Rango insuficiente',
  rankRequired: 'Rango requerido',
  yourRank: 'Tu rango',
  availableForYourRank: 'Disponible para tu rango',
  
  // Generate Case
  generateCase: 'Generar Caso',
  generateNewCase: 'Generar Nuevo Caso',
  caseGeneration: 'Generación de Casos',
  caseLocation: 'Ubicación del Caso',
  incidentDateTime: 'Fecha y Hora del Incidente',
  casePitch: 'Descripción del Caso',
  caseTwist: 'Giros Argumentales',
  caseDifficulty: 'Dificultad',
  targetDuration: 'Duración Objetivo (minutos)',
  constraints: 'Restricciones',
  timezone: 'Zona Horaria',
  generateImages: 'Generar Imágenes',
  generateCaseBtn: 'Generar Caso',
  generatingCase: 'Generando caso...',
  caseGeneratedSuccess: '¡Caso generado exitosamente!',
  caseGenerationError: 'Error al generar caso',
  fillRequiredFields: 'Por favor complete todos los campos requeridos',
  easy: 'Fácil',
  medium: 'Medio',
  hard: 'Difícil',
  caseGenerationForm: 'Formulario de Generación de Casos',
  caseGenerationDesc: 'Seleccione el nivel de dificultad y generaremos un caso de investigación completo para usted.',
  caseGenerationSimpleDesc: 'Solo elija un nivel de dificultad y crearemos un caso atractivo con todos los detalles.',
  backToDashboard: 'Volver al Panel',
  selectDifficulty: 'Seleccionar Dificultad del Caso',
  difficultyHelpText: 'Elija el nivel de complejidad para su caso de investigación',
};