import type { Translations } from '../types/i18n';

export const ptBR: Translations = {
  // Common
  loading: 'Carregando...',
  error: 'Erro',
  success: 'Sucesso',
  cancel: 'Cancelar',
  save: 'Salvar',
  delete: 'Excluir',
  edit: 'Editar',
  close: 'Fechar',
  back: 'Voltar',
  next: 'Próximo',
  previous: 'Anterior',
  
  // Navigation
  home: 'Início',
  dashboard: 'Painel',
  login: 'Entrar',
  register: 'Registrar',
  logout: 'Sair',
  
  // Authentication
  username: 'Nome de usuário',
  email: 'E-mail',
  password: 'Senha',
  confirmPassword: 'Confirmar senha',
  firstName: 'Nome',
  lastName: 'Sobrenome',
  department: 'Departamento',
  badgeNumber: 'Número do crachá',
  rank: 'Posto',
  specialization: 'Especialização',
  
  // Authentication messages
  loginSuccess: 'Login realizado com sucesso',
  loginError: 'Erro ao fazer login',
  registerSuccess: 'Registro realizado com sucesso',
  registerError: 'Erro ao registrar',
  logoutSuccess: 'Logout realizado com sucesso',
  
  // Dashboard
  welcomeBack: 'Bem-vindo de volta',
  availableCases: 'Casos disponíveis',
  recentActivity: 'Atividade recente',
  caseProgress: 'Progresso do caso',
  profile: 'Perfil',
  settings: 'Configurações',
  language: 'Idioma',
  
  // Cases
  caseNumber: 'Número do caso',
  caseTitle: 'Título do caso',
  caseDescription: 'Descrição do caso',
  caseStatus: 'Status do caso',
  caseType: 'Tipo do caso',
  casePriority: 'Prioridade do caso',
  caseAssigned: 'Atribuído a',
  caseCreated: 'Criado em',
  caseUpdated: 'Atualizado em',
  
  // Case statuses
  statusOpen: 'Aberto',
  statusInProgress: 'Em andamento',
  statusClosed: 'Fechado',
  statusSuspended: 'Suspenso',
  
  // Case types
  homicide: 'Homicídio',
  theft: 'Roubo',
  fraud: 'Fraude',
  cybercrime: 'Crime cibernético',
  narcotics: 'Narcóticos',
  
  // Case priorities
  priorityLow: 'Baixa',
  priorityMedium: 'Média',
  priorityHigh: 'Alta',
  priorityCritical: 'Crítica',
  
  // Dashboard specific
  performanceStats: 'Estatísticas de Performance',
  casesResolved: 'Casos Resolvidos',
  casesActive: 'Casos Ativos',
  successRate: 'Taxa de Sucesso',
  averageRating: 'Avaliação Média',
  openWorkspace: 'Abrir Ambiente de Trabalho',
  weeklyGoals: 'Objetivos Semanais',
  recentHistory: 'Histórico Recente',
  lastSession: 'Última sessão',
  priority: 'Prioridade',
  
  // Desktop
  evidence: 'Evidências',
  suspects: 'Suspeitos',
  witnesses: 'Testemunhas',
  forensics: 'Perícia',
  timeline: 'Linha do tempo',
  notes: 'Anotações',
  
  // Evidence
  evidenceNumber: 'Número da evidência',
  evidenceType: 'Tipo de evidência',
  evidenceDescription: 'Descrição da evidência',
  evidenceLocation: 'Local da evidência',
  evidenceCollected: 'Coletada em',
  
  // About
  aboutTitle: 'Sobre o CaseZero Alternative',
  aboutDescription: 'Sistema de investigação policial realista',
  
  // Footer
  currentLanguage: 'Idioma atual',
  currentCase: 'Caso atual',
  
  // Departments
  homicideDept: 'Homicídios',
  theftDept: 'Roubos e Furtos',
  fraudDept: 'Crimes Financeiros',
  cybercrimeDept: 'Crimes Cibernéticos',
  narcoticsDept: 'Narcóticos',
  
  // Ranks
  detective: 'Detetive',
  inspector: 'Inspetor',
  sergeant: 'Sargento',
  specialist: 'Especialista',
  analyst: 'Analista',
  
  // Home page
  heroTitle: 'Sistema de Investigação Policial CaseZero',
  heroSubtitle: 'Investigação Realística de Crimes',
  heroDescription: 'Entre no mundo da investigação criminal com casos realistas, evidências autênticas e procedimentos policiais verdadeiros.',
  getStarted: 'Começar',
  learnMore: 'Saiba mais',
  
  // Features
  featuresTitle: 'Funcionalidades',
  realisticInvestigation: 'Investigação Realística',
  realisticInvestigationDesc: 'Casos baseados em procedimentos policiais reais',
  evidenceAnalysis: 'Análise de Evidências',
  evidenceAnalysisDesc: 'Sistema completo de análise forense',
  caseManagement: 'Gestão de Casos',
  caseManagementDesc: 'Organize e gerencie múltiplos casos simultaneamente',
  
  // Feature list items
  authenticPoliceInterface: 'Interface de computador policial autêntica',
  multipleCases: 'Múltiplos casos para resolver',
  detectiveProgression: 'Progressão do detetive baseada em performance',
  forensicAnalysis: 'Análise forense e coleta de pistas',
  
  // Login page specific
  systemAccess: 'Acesso ao Sistema',
  metropolitanPoliceDept: 'Metropolitan Police Department',
  emailOrId: 'Email ou Número de Identificação',
  enterPassword: 'Digite sua senha',
  enterSystem: 'Entrar no Sistema',
  authenticating: 'Autenticando...',
  noAccess: 'Não possui acesso ao sistema?',
  requestRegistration: 'Solicitar Registro',
  testCredentials: 'Credenciais de Teste:',
  backToHome: 'Voltar ao início',
  
  // Register page specific
  registrationRequest: 'Solicitação de Registro',
  importantNote: 'Importante:',
  systemRestricted: 'Este sistema é restrito a funcionários autorizados do departamento de polícia. Todas as solicitações passam por verificação antes da aprovação.',
  institutionalEmail: 'Email Institucional',
  phoneNumber: 'Telefone',
  badgeNumberField: 'Número do Distintivo',
  selectOption: 'Selecione...',
  investigationDivision: 'Divisão de Investigação',
  criminalForensics: 'Perícia Criminal',
  cybercrimes: 'Crimes Cibernéticos',
  homicides: 'Homicídios',
  frauds: 'Fraudes',
  position: 'Cargo',
  minimumChars: 'Mínimo 8 caracteres',
  confirmYourPassword: 'Confirme sua senha',
  sendingRequest: 'Enviando solicitação...',
  requestRegistrationBtn: 'Solicitar Registro',
  alreadyHaveAccess: 'Já possui acesso ao sistema?',
  doLogin: 'Fazer Login',
  passwordsDontMatch: 'As senhas não coincidem!',
  registrationSent: 'Solicitação de registro enviada com sucesso! Aguarde aprovação do administrador.',
  unexpectedError: 'Erro inesperado. Tente novamente.',
  
  // Error handling
  networkError: 'Erro de conexão. Verifique sua internet.',
  serverError: 'Erro interno do servidor. Tente novamente mais tarde.',
  notFoundError: 'Recurso não encontrado.',
  unauthorizedError: 'Acesso negado. Faça login novamente.',
  forbiddenError: 'Você não tem permissão para esta ação.',
  validationError: 'Dados inválidos. Verifique os campos.',
  timeoutError: 'Tempo limite excedido. Tente novamente.',
  genericError: 'Algo deu errado. Tente novamente.',
  errorOccurred: 'Ocorreu um erro',
  tryAgain: 'Tentar novamente',
  
  // Loading states
  loadingData: 'Carregando dados...',
  loadingCases: 'Carregando casos...',
  loadingProfile: 'Carregando perfil...',
  processing: 'Processando...',
  saving: 'Salvando...',
  uploading: 'Enviando...',
  pleaseWait: 'Por favor, aguarde...',
  almostDone: 'Quase pronto...',
  
  // Offline support
  offline: 'Offline',
  online: 'Online',
  offlineMessage: 'Você está offline. Algumas funcionalidades podem estar limitadas.',
  connectionRestored: 'Conexão restaurada!',
  workingOffline: 'Trabalhando offline',
  
  // Keyboard navigation
  keyboardShortcuts: 'Atalhos de teclado',
  pressEscToClose: 'Pressione Esc para fechar',
  pressEnterToSelect: 'Pressione Enter para selecionar',
  useArrowKeys: 'Use as setas para navegar',
  pressTabToNavigate: 'Pressione Tab para navegar',
  
  // New landing page content
  digitalInterface: 'Interface Digital',
  careerProgression: 'Progressão na Carreira',
  justiceSystem: 'Sistema de Justiça',
  justiceSystemDesc: 'Experimente o processo completo de investigação até condenação',
  
  // Game mechanics
  howItWorks: 'Como Funciona',
  howItWorksDesc: 'Experimente procedimentos autênticos de investigação policial através de jogabilidade imersiva',
  investigation: 'Investigação',
  investigationDesc: 'Entreviste testemunhas, analise cenas de crime',
  forensicsDesc: 'Processe evidências no laboratório',
  documentation: 'Documentação',
  documentationDesc: 'Mantenha arquivos detalhados de casos',
  realTime: 'Tempo Real',
  realTimeDesc: 'Casos evoluem ao longo do tempo',
  
  // Call to action
  readyToSolve: 'Pronto para Resolver Seu Primeiro Caso?',
  ctaDescription: 'Junte-se ao sistema de investigação do Departamento de Polícia Metropolitana e inicie sua carreira como detetive. Acesso restrito apenas a pessoal autorizado.',
  accessSystem: 'Acessar Sistema',
  requestAccess: 'Solicitar Acesso',
  
  // New registration form translations
  newRegistrationInfo: 'Para o registro você precisa apenas de nome, sobrenome e email pessoal. Seu email institucional, departamento, posição e número de distintivo serão gerados automaticamente.',
  institutionalEmailPreview: '🎯 Seu email institucional será:',
  personalEmail: 'Email Pessoal',
  submitting: 'Enviando...',
  requestRegistrationBtn2: 'Solicitar Registro',
  institutionalEmailInfo: 'Seu email institucional será:',
  useInstitutionalLogin: 'Para fazer login, você deverá usar seu email institucional ao invés do email pessoal.',
  
  // Evidence visibility and forensics
  evidenceVisibility: 'Visibilidade da Evidência',
  visibleEvidences: 'Evidências Visíveis',
  hiddenEvidences: 'Evidências Ocultas',
  makeVisible: 'Tornar Visível',
  makeHidden: 'Ocultar',
  visibilityUpdated: 'Visibilidade atualizada com sucesso',
  forensicAnalysisTitle: 'Análise Forense',
  requestAnalysis: 'Solicitar Análise',
  analysisType: 'Tipo de Análise',
  analysisInProgress: 'Análise em Andamento',
  analysisCompleted: 'Análise Concluída',
  analysisResults: 'Resultados da Análise',
  noAnalysisAvailable: 'Nenhuma análise disponível para esta evidência',
  timeBasedAnalysis: 'Análise baseada em tempo',
  estimatedCompletion: 'Conclusão estimada',
  analysisTypeNotSupported: 'Tipo de análise não suportado para esta evidência',
  selectAnalysisType: 'Selecione o tipo de análise',
  supportedAnalyses: 'Análises suportadas',
  
  // Analysis types
  dnaAnalysis: 'Análise de DNA',
  fingerprintAnalysis: 'Análise de Impressões Digitais',
  digitalForensics: 'Perícia Digital',
  ballisticsAnalysis: 'Análise Balística',
  toxicologyAnalysis: 'Análise Toxicológica',
  handwritingAnalysis: 'Análise Grafotécnica',
  voiceAnalysis: 'Análise de Voz',
  traceAnalysis: 'Análise de Vestígios',
  generalAnalysis: 'Análise Geral',
  
  // Case processing
  caseProcessing: 'Processamento de Casos',
  processingAllCases: 'Processando todos os casos',
  processingCase: 'Processando caso',
  caseProcessed: 'Caso processado com sucesso',
  caseAlreadyProcessed: 'Caso já foi processado',
  processingStatus: 'Status do processamento',
  
  // Rank-based access
  accessDenied: 'Acesso negado',
  insufficientRank: 'Rank insuficiente',
  rankRequired: 'Rank necessário',
  yourRank: 'Seu rank',
  availableForYourRank: 'Disponível para seu rank',
  
  // Generate Case
  generateCase: 'Gerar Caso',
  generateNewCase: 'Gerar Novo Caso',
  caseGeneratorAI: 'GERADOR DE CASOS IA',
  caseGeneration: 'Geração de Casos',
  caseLocation: 'Local do Caso',
  incidentDateTime: 'Data e Hora do Incidente',
  casePitch: 'Descrição do Caso',
  caseTwist: 'Reviravoltas',
  caseDifficulty: 'Dificuldade',
  targetDuration: 'Duração Estimada (minutos)',
  constraints: 'Restrições',
  timezone: 'Fuso Horário',
  generateImages: 'Gerar Imagens',
  generateCaseBtn: 'Gerar Caso',
  generatingCase: 'Gerando caso...',
  caseGeneratedSuccess: 'Caso gerado com sucesso!',
  caseGenerationError: 'Erro ao gerar caso',
  fillRequiredFields: 'Preencha todos os campos obrigatórios',
  easy: 'Fácil',
  medium: 'Médio',
  hard: 'Difícil',
  caseGenerationForm: 'Formulário de Geração de Casos',
  caseGenerationDesc: 'Selecione o nível de dificuldade e nós geraremos um caso de investigação completo para você.',
  caseGenerationSimpleDesc: 'Apenas escolha um nível de dificuldade e criaremos um caso envolvente com todos os detalhes.',
  backToDashboard: 'Voltar ao Painel',
  selectDifficulty: 'Selecionar Dificuldade do Caso',
  difficultyHelpText: 'Escolha o nível de complexidade para seu caso de investigação',
  // Case Generator AI
  caseGeneratorTitle: 'Gerador de Casos IA',
  caseGeneratorDescription: 'Gere casos de investigação realistas usando inteligência artificial. Acompanhe o progresso em tempo real enquanto a IA cria documentos, evidências e cenários.',
  startGeneration: 'Iniciar Geração',
  generationProgress: 'Progresso da Geração',
  currentStep: 'Etapa Atual',
  estimatedTime: 'Tempo Estimado',
  completed: 'Concluído',
  failed: 'Falhou',
  running: 'Executando',
  // Generation Steps
  stepPlan: 'Planejando estrutura do caso',
  stepExpand: 'Expandindo detalhes do caso',
  stepDesign: 'Projetando fluxo de investigação',
  stepGenDocs: 'Gerando documentos',
  stepGenMedia: 'Criando recursos de mídia',
  stepNormalize: 'Normalizando conteúdo',
  stepIndex: 'Indexando dados do caso',
  stepRuleValidate: 'Validando regras',
  stepRedTeam: 'Controle de qualidade',
  stepPackage: 'Empacotando caso final',
  
  // Pinboard
  pinboard: 'Quadro de Investigação',
  detectivePinboard: 'Quadro de Investigação do Detetive',
  pinboardTitle: 'Quadro de Investigação do Caso',
  addEvidence: 'Adicionar Evidência',
  addNote: 'Adicionar Nota',
  addPhoto: 'Adicionar Foto',
  evidenceItem: 'Evidência',
  noteItem: 'Nota',
  photoItem: 'Foto',
  dragToOrganize: 'Arraste para organizar',
  connectEvidence: 'Conectar Evidências',
  clearBoard: 'Limpar Quadro',
  saveBoard: 'Salvar Quadro',
  loadBoard: 'Carregar Quadro',
  evidenceConnected: 'Evidências conectadas',
  evidenceDisconnected: 'Evidências desconectadas',
  boardCleared: 'Quadro limpo',
  boardSaved: 'Quadro salvo',
  boardLoaded: 'Quadro carregado',
  deleteItem: 'Excluir Item',
  editItem: 'Editar Item',
  itemDeleted: 'Item excluído',
  noItemsOnBoard: 'Nenhum item no quadro',
  dropHereToAdd: 'Solte aqui para adicionar',
};