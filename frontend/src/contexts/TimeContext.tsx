import React, { createContext, useState, useEffect, useRef } from 'react'
import type { ReactNode } from 'react'

interface TimeContextType {
  gameTime: Date
  startTime: Date
  isRunning: boolean
  setStartTime: (startTime: Date) => void
  pauseTime: () => void
  resumeTime: () => void
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
}

// Game time runs faster than real time: 1 real second = 1 game minute (60x multiplier)
// This means: 1 real minute = 1 game hour, 1 real hour = 60 game hours
const TIME_MULTIPLIER = 60

export const TimeProvider: React.FC<TimeProviderProps> = ({ children, caseId, initialGameTime }) => {
  const [gameTime, setGameTime] = useState<Date>(initialGameTime || new Date())
  const [startTime, setStartTimeState] = useState<Date>(initialGameTime || new Date())
  const [isRunning, setIsRunning] = useState<boolean>(true)
  const [timeEntries, setTimeEntries] = useState<TimeEntry[]>([])
  const intervalRef = useRef<number | null>(null)
  const lastUpdateRef = useRef<number>(Date.now())

  // Initialize game time based on case
  useEffect(() => {
    if (caseId && !initialGameTime) {
      // Only set default time if no initialGameTime was provided
      const caseStartTime = new Date()
      // Set to 8:00 AM of today for the case start
      caseStartTime.setHours(8, 0, 0, 0)
      setStartTimeState(caseStartTime)
      setGameTime(new Date(caseStartTime))
      
      // Add initial case start entry
      const initialEntry: TimeEntry = {
        id: `init-${Date.now()}`,
        timestamp: caseStartTime,
        type: 'case',
        message: `Investigação iniciada - Caso ${caseId}`,
        category: 'Início do Caso',
        priority: 'high'
      }
      setTimeEntries([initialEntry])
    } else if (caseId && initialGameTime) {
      // Use provided initialGameTime (from previous session)
      const resumeEntry: TimeEntry = {
        id: `resume-${Date.now()}`,
        timestamp: initialGameTime,
        type: 'case',
        message: `Investigação retomada - Caso ${caseId}`,
        category: 'Retomada do Caso',
        priority: 'medium'
      }
      setTimeEntries(prev => [...prev, resumeEntry])
    }
  }, [caseId, initialGameTime])

  // Game time ticker
  useEffect(() => {
    if (isRunning) {
      intervalRef.current = setInterval(() => {
        const now = Date.now()
        const realTimeDelta = now - lastUpdateRef.current
        const gameTimeDelta = realTimeDelta * TIME_MULTIPLIER
        
        setGameTime(prev => new Date(prev.getTime() + gameTimeDelta))
        lastUpdateRef.current = now
      }, 1000) // Update every second
    } else {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
      }
    }
  }, [isRunning])

  // Update last update reference when resuming
  useEffect(() => {
    if (isRunning) {
      lastUpdateRef.current = Date.now()
    }
  }, [isRunning])

  const setStartTime = (newStartTime: Date) => {
    setStartTimeState(newStartTime)
    setGameTime(new Date(newStartTime))
  }

  const pauseTime = () => {
    setIsRunning(false)
  }

  const resumeTime = () => {
    setIsRunning(true)
    lastUpdateRef.current = Date.now()
  }

  const addTimeEntry = (entry: TimeEntry) => {
    setTimeEntries(prev => [...prev, entry].sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime()))
  }

  const getFormattedTime = () => {
    return gameTime.toLocaleTimeString('pt-BR', { 
      hour: '2-digit', 
      minute: '2-digit',
      second: '2-digit'
    })
  }

  const getFormattedDate = () => {
    return gameTime.toLocaleDateString('pt-BR', {
      day: '2-digit',
      month: '2-digit', 
      year: 'numeric'
    })
  }

  const contextValue: TimeContextType = {
    gameTime,
    startTime,
    isRunning,
    setStartTime,
    pauseTime,
    resumeTime,
    addTimeEntry,
    timeEntries,
    getFormattedTime,
    getFormattedDate
  }

  return (
    <TimeContext.Provider value={contextValue}>
      {children}
    </TimeContext.Provider>
  )
}

export default TimeContext