import React, { createContext, useContext, useEffect, useRef, useSyncExternalStore, useState, useCallback } from 'react'
import type { ReactNode } from 'react'
import * as signalR from '@microsoft/signalr'
import { CaseEngine } from '../engine/CaseEngine'
import type { EngineState, ApiClient, SignalRClient } from '../engine/CaseEngine'
import type { SubmitCaseRequest, SubmitCaseResult } from '../types/caseV2'
import { casesV2Api, tokenStorage } from '../services/api'

const makeApiClient = (): ApiClient => ({
  getCase: (caseId) => casesV2Api.getCase(caseId),
  viewAsset: (caseId, assetId) => casesV2Api.viewAsset(caseId, assetId),
  openEmail: (caseId, emailId) => casesV2Api.openEmail(caseId, emailId),
  viewSuspect: (caseId, suspectId) => casesV2Api.viewSuspect(caseId, suspectId),
  submitCase: (caseId, payload) => casesV2Api.submitCase(caseId, payload),
  postGameTime: (caseId, gameTimeMinutes) => casesV2Api.postGameTime(caseId, gameTimeMinutes),
})

const makeSignalRClient = (): SignalRClient => {
  let connection: signalR.HubConnection | null = null
  return {
    async connect(token: string) {
      const apiBase = import.meta.env.VITE_API_URL || 'http://localhost:5001'
      const baseUrl = apiBase.replace(/\/api$/, '')
      connection = new signalR.HubConnectionBuilder()
        .withUrl(`${baseUrl}/hubs/forensics`, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .build()
      await connection.start()
    },
    async disconnect() {
      await connection?.stop()
      connection = null
    },
    on(event, callback) {
      connection?.on(event, callback)
      return () => connection?.off(event, callback)
    }
  }
}

interface CaseContextValue {
  state: EngineState
  caseId: string | null
  // Back-compat alias for legacy components
  currentCase: string | null
  // Truthy marker indicating a case is loaded (for legacy components checking engine truthiness)
  engine: object | null
  isLoading: boolean
  error: string | null
  loadCase: (caseId: string) => Promise<void>
  viewAsset: (assetId: string) => Promise<void>
  openEmail: (emailId: string) => Promise<void>
  viewSuspect: (suspectId: string) => Promise<void>
  submitCase: (payload: SubmitCaseRequest) => Promise<SubmitCaseResult>
  postGameTime: (min: number) => Promise<void>
  // Back-compat: legacy TimeSync uses this. Accepts a Date.
  updateGameTime: (newTime: Date) => void
}

const CaseContext = createContext<CaseContextValue | undefined>(undefined)

export const CaseProvider: React.FC<{ children: ReactNode; caseId?: string }> = ({ children, caseId }) => {
  const engineRef = useRef<CaseEngine | null>(null)
  if (!engineRef.current) {
    engineRef.current = new CaseEngine(makeApiClient(), makeSignalRClient())
  }
  const engine = engineRef.current

  const state = useSyncExternalStore(
    (cb) => engine.subscribe(cb),
    () => engine.getSnapshot()
  )

  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activeCaseId, setActiveCaseId] = useState<string | null>(caseId || null)
  const sessionStartRef = useRef<Date | null>(null)

  // Load case when caseId changes
  useEffect(() => {
    if (!caseId) return
    setActiveCaseId(caseId)
    setIsLoading(true)
    setError(null)
    engine.loadCase(caseId)
      .then(() => setIsLoading(false))
      .catch((err: Error) => {
        setError(err.message || 'Failed to load case')
        setIsLoading(false)
      })
  }, [caseId, engine])

  // Connect to SignalR
  useEffect(() => {
    if (!caseId) return
    const token = tokenStorage.get()
    if (!token) return

    const srClient = makeSignalRClient()
    let disposed = false
    const cleanups: Array<() => void> = []

    srClient.connect(token)
      .then(() => {
        if (disposed) return
        cleanups.push(srClient.on('case.entity.revealed', (data: unknown) => {
          const d = data as { entityType: 'email' | 'asset' | 'suspect'; entityId: string }
          engine.applyReveal(d.entityType, d.entityId)
        }))
        cleanups.push(srClient.on('case.notification', (data: unknown) => {
          const d = data as { level: 'info' | 'warn' | 'critical'; message: string }
          engine.pushNotification(d)
        }))
        cleanups.push(srClient.on('case.email.attached', (data: unknown) => {
          const d = data as { emailId: string; assetId: string }
          engine.applyReveal('asset', d.assetId)
        }))
      })
      .catch((err: Error) => console.error('SignalR connect failed:', err))

    return () => {
      disposed = true
      cleanups.forEach(fn => fn())
      srClient.disconnect().catch(console.error)
    }
  }, [caseId, engine])

  const loadCase = useCallback(async (id: string) => {
    setActiveCaseId(id)
    setIsLoading(true)
    setError(null)
    try {
      await engine.loadCase(id)
    } catch (err) {
      setError((err as Error).message || 'Failed to load case')
    } finally {
      setIsLoading(false)
    }
  }, [engine])

  const updateGameTime = useCallback((newTime: Date) => {
    // Legacy callers pass a Date; we translate to minutes since session start
    // and forward to the engine.postGameTime endpoint.
    if (!sessionStartRef.current) {
      sessionStartRef.current = newTime
      return
    }
    const minutes = Math.max(
      0,
      Math.floor((newTime.getTime() - sessionStartRef.current.getTime()) / 60000)
    )
    void engine.postGameTime(minutes)
  }, [engine])

  const contextValue: CaseContextValue = {
    state,
    caseId: activeCaseId,
    currentCase: activeCaseId,
    engine: state.case ? state.case : null,
    isLoading,
    error,
    loadCase,
    viewAsset: (id) => engine.viewAsset(id),
    openEmail: (id) => engine.openEmail(id),
    viewSuspect: (id) => engine.viewSuspect(id),
    submitCase: (payload) => engine.submitCase(payload),
    postGameTime: (min) => engine.postGameTime(min),
    updateGameTime,
  }

  return (
    <CaseContext.Provider value={contextValue}>
      {children}
    </CaseContext.Provider>
  )
}

export function useCase() {
  const ctx = useContext(CaseContext)
  if (!ctx) throw new Error('useCase must be used within a CaseProvider')
  return ctx
}

export function useEmails() {
  return useCase().state.visibleEmails
}

export function useAssets() {
  return useCase().state.visibleAssets
}

export function useSuspects() {
  return useCase().state.visibleSuspects
}

export function useSubmission() {
  return useCase().state.submission
}

export function useNotifications() {
  return useCase().state.notifications
}

export default CaseContext
