import { useEffect } from 'react'
import { useTimeContext } from '../hooks/useTimeContext'
import { useCase } from '../hooks/useCaseContext'

/**
 * TimeSync Component - Sincroniza o TimeContext com o CaseEngine
 * 
 * Este componente não renderiza nada, apenas sincroniza o tempo do jogo
 * entre o TimeContext (que controla o relógio) e o CaseEngine (que gerencia
 * eventos temporais e perícias).
 */
export const TimeSync: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { elapsedMinutes } = useTimeContext()
  const { postGameTime, engine } = useCase()
  const minutes = Math.floor(elapsedMinutes)

  // Sincroniza o gameTime do TimeContext com o CaseEngine
  useEffect(() => {
    if (engine) {
      void postGameTime(minutes)
    }
  }, [minutes, engine, postGameTime])

  return <>{children}</>
}
