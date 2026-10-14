import React, { createContext, useState, useEffect, useRef, useCallback } from 'react'
import type { ReactNode } from 'react'
import { caseSessionApi, type GameClockSnapshot } from '../services/api'
import { useLanguage } from '../hooks/useLanguageContext'

interface TimeContextType {
  gameTime: Date
  startTime: Date
  elapsedMinutes: number
  isRunning: boolean
  error: string | null
  setStartTime: (startTime: Date) => void
  pauseTime: () => Promise<void>
  resumeTime: () => Promise<void>
  addTimeEntry: (entry: TimeEntry) => void
  timeEntries: TimeEntry[]
  getFormattedTime: () => string
  getFormattedDate: () => string
}

export interface TimeEntry {
  id: string
  timestamp: Date
  type: 'system' | 'forensics' | 'user' | 'case'
  message: string
  category?: string
  priority?: 'low' | 'medium' | 'high' | 'critical'
}

const TimeContext = createContext<TimeContextType | undefined>(undefined)

interface TimeProviderProps {
  children: ReactNode
  caseId?: string
  initialGameTime?: Date
  initialGameClock?: GameClockSnapshot
}

export const TimeProvider: React.FC<TimeProviderProps> = ({ children, caseId, initialGameTime, initialGameClock }) => {
  const { t, currentLanguage } = useLanguage()
  const [gameTime, setGameTime] = useState(() => new Date(initialGameClock?.gameTime ?? initialGameTime?.getTime() ?? Date.now()))
  const [elapsedMinutes, setElapsedMinutes] = useState(initialGameClock?.elapsedMinutes ?? 0)
  const [isRunning, setIsRunning] = useState(initialGameClock?.isRunning ?? false)
  const [error, setError] = useState<string | null>(null)
  const [timeEntries, setTimeEntries] = useState<TimeEntry[]>([])
  const snapshot = useRef({ clock: initialGameClock, receivedAt: Date.now() })
  const clockChanges = useRef(0)

  const applyClock = useCallback((clock: GameClockSnapshot) => {
    const previous = snapshot.current.clock
    if (previous && Date.parse(clock.asOfUtc) < Date.parse(previous.asOfUtc)) return
    snapshot.current = { clock, receivedAt: Date.now() }
    setGameTime(new Date(clock.gameTime))
    setElapsedMinutes(clock.elapsedMinutes)
    setIsRunning(clock.isRunning)
    setError(null)
  }, [])

  useEffect(() => {
    if (initialGameClock) applyClock(initialGameClock)
  }, [initialGameClock, applyClock])

  useEffect(() => {
    if (!caseId) return
    let alive = true
    const sync = async () => {
      const change = clockChanges.current
      try {
        const clock = await caseSessionApi.getGameClock(caseId)
        if (alive && change === clockChanges.current) applyClock(clock)
      } catch (err) {
        console.error('Game clock synchronization failed:', err)
        if (alive && change === clockChanges.current) setError(t('gameClockSyncFailed'))
      }
    }
    void sync()
    const interval = setInterval(() => { void sync() }, 10000)
    return () => { alive = false; clearInterval(interval) }
  }, [caseId, applyClock, t])

  useEffect(() => {
    const interval = setInterval(() => {
      const clock = snapshot.current.clock
      if (!clock) return
      const delta = clock.isRunning ? Math.max(0, Date.now() - snapshot.current.receivedAt) * clock.multiplier : 0
      setGameTime(new Date(new Date(clock.gameTime).getTime() + delta))
      setElapsedMinutes(clock.elapsedMinutes + delta / 60000)
    }, 1000)
    return () => clearInterval(interval)
  }, [])

  useEffect(() => {
    if (!caseId) return
    const pauseOnExit = () => {
      void caseSessionApi.pauseGameClock(caseId, true).catch((err: unknown) => {
        console.error('Saving the case clock on page exit failed:', err)
      })
    }
    window.addEventListener('pagehide', pauseOnExit)
    return () => window.removeEventListener('pagehide', pauseOnExit)
  }, [caseId])

  const changeClock = async (running: boolean) => {
    if (!caseId) return
    try {
      const session = await (running ? caseSessionApi.resumeGameClock(caseId) : caseSessionApi.pauseGameClock(caseId))
      if (!session.gameClock) throw new Error('Missing authoritative game clock')
      clockChanges.current += 1
      applyClock(session.gameClock)
    } catch (err) {
      console.error('Changing game clock state failed:', err)
      setError(t('gameClockSyncFailed'))
    }
  }

  const startTime = new Date(gameTime.getTime() - elapsedMinutes * 60000)
  return (
    <TimeContext.Provider value={{
      gameTime, startTime, elapsedMinutes, isRunning, error,
      setStartTime: () => { setError(t('gameClockServerOwned')) },
      pauseTime: () => changeClock(false), resumeTime: () => changeClock(true),
      addTimeEntry: entry => setTimeEntries(prev => [...prev, entry].sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime())),
      timeEntries,
      getFormattedTime: () => gameTime.toLocaleTimeString(currentLanguage, { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
      getFormattedDate: () => gameTime.toLocaleDateString(currentLanguage, { day: '2-digit', month: '2-digit', year: 'numeric' }),
    }}>
      {children}
    </TimeContext.Provider>
  )
}

export default TimeContext
