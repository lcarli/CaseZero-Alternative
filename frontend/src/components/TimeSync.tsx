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
  const { gameTime } = useTimeContext()
  const { updateGameTime, engine } = useCase()

  // Sincroniza o gameTime do TimeContext com o CaseEngine
  useEffect(() => {
    if (engine && gameTime) {
      updateGameTime(gameTime)
    }
  }, [gameTime, engine, updateGameTime])

  return <>{children}</>
}
