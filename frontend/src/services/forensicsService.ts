import type { TimeEntry } from '../contexts/TimeContext'

export interface ForensicsRequest {
  id: string
  type: ForensicsType
  fileName: string
  submittedAt: Date
  estimatedCompletionTime: Date
  status: 'pending' | 'processing' | 'completed'
  results?: string
}

export type ForensicsType = 'fingerprint' | 'dna' | 'ballistics' | 'chemical' | 'digital'

// Forensics timing in minutes (game time)
export const FORENSICS_TIMING: Record<ForensicsType, number> = {
  fingerprint: 30,     // 30 minutes
  dna: 120,           // 2 hours  
  ballistics: 90,     // 1.5 hours
  chemical: 180,      // 3 hours
  digital: 60         // 1 hour
}

export class ForensicsService {
  private requests: Map<string, ForensicsRequest> = new Map()
  private onRequestComplete?: (request: ForensicsRequest) => void
  private onAddTimeEntry?: (entry: TimeEntry) => void

  constructor(
    onRequestComplete?: (request: ForensicsRequest) => void,
    onAddTimeEntry?: (entry: TimeEntry) => void
  ) {
    this.onRequestComplete = onRequestComplete
    this.onAddTimeEntry = onAddTimeEntry
  }

  submitForensicsRequest(
    type: ForensicsType, 
    fileName: string, 
    currentTime: Date
  ): ForensicsRequest {
    const id = `forensics-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`
    const timingMinutes = FORENSICS_TIMING[type]
    const estimatedCompletionTime = new Date(currentTime.getTime() + (timingMinutes * 60 * 1000))
    
    const request: ForensicsRequest = {
      id,
      type,
      fileName,
      submittedAt: currentTime,
      estimatedCompletionTime,
      status: 'pending'
    }

    this.requests.set(id, request)

    // Add log entry for submission
    if (this.onAddTimeEntry) {
      this.onAddTimeEntry({
        id: `forensics-submit-${id}`,
        timestamp: currentTime,
        type: 'forensics',
        message: `Arquivo "${fileName}" enviado para análise ${this.getTypeDescription(type)}`,
        category: 'Perícia',
        priority: 'medium'
      })
    }

    // Schedule completion
    this.scheduleCompletion(request)

    return request
  }

  private scheduleCompletion(request: ForensicsRequest) {
    const delay = request.estimatedCompletionTime.getTime() - request.submittedAt.getTime()
    
    setTimeout(() => {
      request.status = 'completed'
      request.results = this.generateMockResults(request.type, request.fileName)
      
      // Add log entry for completion
      if (this.onAddTimeEntry) {
        this.onAddTimeEntry({
          id: `forensics-complete-${request.id}`,
          timestamp: new Date(),
          type: 'forensics',
          message: `Resultado da análise ${this.getTypeDescription(request.type)} disponível: "${request.fileName}"`,
          category: 'Perícia',
          priority: 'high'
        })
      }

      if (this.onRequestComplete) {
        this.onRequestComplete(request)
      }
    }, delay)
  }

  getActiveRequests(): ForensicsRequest[] {
    return Array.from(this.requests.values()).filter(req => req.status !== 'completed')
  }

  getCompletedRequests(): ForensicsRequest[] {
    return Array.from(this.requests.values()).filter(req => req.status === 'completed')
  }

  getAllRequests(): ForensicsRequest[] {
    return Array.from(this.requests.values())
  }

  private getTypeDescription(type: ForensicsType): string {
    switch (type) {
      case 'fingerprint': return 'de impressões digitais'
      case 'dna': return 'de DNA'
      case 'ballistics': return 'balística'
      case 'chemical': return 'química'
      case 'digital': return 'digital'
      default: return 'forense'
    }
  }

  private generateMockResults(type: ForensicsType, fileName: string): string {
    switch (type) {
      case 'fingerprint':
        return `Análise de impressões digitais concluída para ${fileName}. 3 impressões parciais identificadas. 2 impressões correspondem ao banco de dados criminal.`
      case 'dna':
        return `Análise de DNA finalizada para ${fileName}. Perfil genético extraído com 99.7% de confiabilidade. Correspondência encontrada no banco de dados.`
      case 'ballistics':
        return `Análise balística de ${fileName} concluída. Projétil calibre .45 ACP. Marcas de raiamento compatíveis com Glock modelo 21.`
      case 'chemical':
        return `Análise química de ${fileName} finalizada. Substância identificada: Cocaína (pureza 87%). Traços de lactose e inositol detectados.`
      case 'digital':
        return `Análise forense digital de ${fileName} concluída. 47 arquivos recuperados. Metadados indicam última modificação em 15/01/2024.`
      default:
        return `Análise forense de ${fileName} concluída. Resultados disponíveis para revisão.`
    }
  }
}

export const forensicsService = new ForensicsService()