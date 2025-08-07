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
};