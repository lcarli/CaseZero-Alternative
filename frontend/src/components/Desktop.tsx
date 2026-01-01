import React, { useEffect, useState } from 'react'
import styled from 'styled-components'
import { useNavigate } from 'react-router-dom'
import Dock from './Dock'
import Window from './Window'
import { useWindowContext } from '../hooks/useWindowContext'
import { useCase } from '../hooks/useCaseContext'
import { useAuth } from '../hooks/useAuthContext'
import { useTimeContext } from '../hooks/useTimeContext'
import { useLanguage } from '../hooks/useLanguageContext'
import { caseSessionApi, assetsApi, emailsApi, forensicsApi } from '../services/api'
import { forensicsSignalR } from '../services/forensicsSignalR'
import logoMetroPolice from '../assets/LogoMetroPolice_transparent.png'

const DesktopContainer = styled.div`
  width: 100vw;
  height: 100vh;
  background-image: 
    radial-gradient(circle at 20% 80%, rgba(52, 152, 219, 0.1) 0%, transparent 50%),
    radial-gradient(circle at 80% 20%, rgba(74, 158, 255, 0.08) 0%, transparent 50%),
    repeating-linear-gradient(90deg, transparent, rgba(52, 152, 219, 0.03) 2px, transparent 4px),
    repeating-linear-gradient(0deg, transparent, rgba(52, 152, 219, 0.03) 2px, transparent 4px),
    linear-gradient(135deg, #0a0f23 0%, #1a2140 25%, #2a3458 50%, #1a2140 75%, #0a0f23 100%);
  position: relative;
  overflow: hidden;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  
  &::before {
    content: '';
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    width: 600px;
    height: 600px;
    background-image: url(${logoMetroPolice});
    background-size: contain;
    background-repeat: no-repeat;
    background-position: center;
    opacity: 0.08;
    pointer-events: none;
    z-index: 0;
  }
`



const SystemInfo = styled.div`
  position: absolute;
  bottom: 100px;
  left: 20px;
  background: rgba(0, 0, 0, 0.8);
  border: 1px solid rgba(52, 152, 219, 0.5);
  border-radius: 6px;
  padding: 8px 12px;
  color: rgba(255, 255, 255, 0.9);
  font-size: 11px;
  font-family: 'Courier New', monospace;
  backdrop-filter: blur(10px);
  z-index: 1;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.8);
  
  div {
    margin-bottom: 2px;
    
    &:last-child {
      margin-bottom: 0;
    }
  }
  
  .label {
    color: rgba(52, 152, 219, 0.9);
    display: inline-block;
    width: 60px;
    font-weight: 600;
  }
`

const DesktopArea = styled.div`
  width: 100%;
  height: calc(100vh - 80px); /* Reserve space for dock */
  position: relative;
  overflow: hidden;
`

const Desktop: React.FC = () => {
  const navigate = useNavigate()
  const { user } = useAuth()
  const { gameTime } = useTimeContext()
  const { t, language } = useLanguage()
  const {
    windows,
    openWindow,
    closeWindow,
    bringToFront,
    updateWindowPosition,
    updateWindowSize,
    maximizeWindow,
    minimizeWindow,
    updateWindowProps,
    isWindowOpen
  } = useWindowContext()

  const { currentCase } = useCase()
  
  // Task 47: State for assets, emails, and forensics
  const [assets, setAssets] = useState<any[]>([])
  const [_emails, setEmails] = useState<any[]>([])
  const [_forensics, setForensics] = useState<any[]>([])
  const [_loading, setLoading] = useState(true)

  // Add desktop-mode class when component mounts, remove when it unmounts
  useEffect(() => {
    document.body.classList.add('desktop-mode')
    return () => {
      document.body.classList.remove('desktop-mode')
    }
  }, [])

  // Task 47: Load session data on mount
  useEffect(() => {
    const loadSessionData = async () => {
      if (!currentCase) return

      try {
        setLoading(true)
        
        // Load assets, emails, and forensics in parallel
        const [assetsData, emailsData, forensicsData] = await Promise.all([
          assetsApi.getAssets(currentCase),
          emailsApi.getEmails(currentCase),
          forensicsApi.getPendingRequests(currentCase)
        ])

        setAssets(assetsData)
        setEmails(emailsData)
        setForensics(forensicsData)

        console.log('✅ Session data loaded:', {
          assets: assetsData.length,
          emails: emailsData.length,
          forensics: forensicsData.length
        })
      } catch (error) {
        console.error('❌ Failed to load session data:', error)
      } finally {
        setLoading(false)
      }
    }

    loadSessionData()
  }, [currentCase])

  // Task 52: SignalR Hub for real-time forensic updates (replaces Task 51 polling)
  useEffect(() => {
    if (!currentCase) return

    const token = localStorage.getItem('token')
    if (!token) {
      console.warn('No token found, cannot connect to SignalR')
      return
    }

    let cleanupListener: (() => void) | null = null

    const initSignalR = async () => {
      try {
        await forensicsSignalR.connect(token)
        console.log('✅ SignalR connected for case:', currentCase)

        // Listen for forensic completion events
        cleanupListener = forensicsSignalR.onForensicCompleted(async (data) => {
          console.log('🔔 Forensic completed notification:', data)
          
          // Refetch forensics and emails
          try {
            const [forensicsData, emailsData] = await Promise.all([
              forensicsApi.getPendingRequests(currentCase),
              emailsApi.getEmails(currentCase)
            ])
            setForensics(forensicsData)
            setEmails(emailsData)
            console.log('✅ Data refreshed after forensic completion')
          } catch (error) {
            console.error('❌ Failed to refresh data after forensic completion:', error)
          }
        })
      } catch (error) {
        console.error('❌ Failed to connect to SignalR:', error)
        // Fallback to polling if SignalR fails
        console.log('⚠️ Falling back to polling mode')
        startPolling()
      }
    }

    // Fallback polling function
    const startPolling = () => {
      const pollForensics = setInterval(async () => {
        try {
          const forensicsData = await forensicsApi.getPendingRequests(currentCase)
          setForensics(forensicsData)
          
          const hasCompleted = forensicsData.some((f: any) => f.status === 'completed')
          if (hasCompleted) {
            const emailsData = await emailsApi.getEmails(currentCase)
            setEmails(emailsData)
          }
        } catch (error) {
          console.error('❌ Failed to poll forensics:', error)
        }
      }, 30000)

      return () => clearInterval(pollForensics)
    }

    initSignalR()

    return () => {
      if (cleanupListener) {
        cleanupListener()
      }
      forensicsSignalR.disconnect()
    }
  }, [currentCase])

  // Task 50: Refetch assets after attachment download
  const refetchAssets = async () => {
    if (!currentCase) return
    try {
      const assetsData = await assetsApi.getAssets(currentCase)
      setAssets(assetsData)
      
      // Update FileViewer props if window is open
      if (isWindowOpen('file-viewer')) {
        updateWindowProps('file-viewer', { assets: assetsData })
      }
    } catch (error) {
      console.error('❌ Failed to refetch assets:', error)
    }
  }

  // Task 48-49: Wrapper to inject props into app windows
  const handleOpenWindow = (id: string, title: string, component: React.ComponentType<any>) => {
    // If opening FileViewer, pass assets and refresh callback
    if (id === 'file-viewer') {
      openWindow(id, title, component, { assets, onRefresh: refetchAssets })
    } 
    // If opening EmailApp, pass emails and caseId
    else if (id === 'email-app') {
      openWindow(id, title, component, { 
        emails: _emails, 
        caseId: currentCase,
        onRefetchAssets: refetchAssets 
      })
    } 
    else {
      openWindow(id, title, component)
    }
  }

  const handleCaseDisconnect = async () => {
    console.log('🚪 Case disconnect button clicked!')
    console.log('📋 Current case:', currentCase)
    console.log('⏰ Current gameTime:', gameTime)
    
    try {
      // Save current session time if we have a case and game time
      if (currentCase && gameTime) {
        const token = localStorage.getItem('token')
        const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5187'
        console.log('🔑 Token exists:', !!token)
        console.log('🌐 API URL:', `${apiUrl}/api/casesession/end/${currentCase}`)
        console.log('📡 Calling endSession API...')
        
        const result = await caseSessionApi.endSession(currentCase, {
          gameTimeAtEnd: gameTime.toISOString()
        })
        console.log('✅ Session saved successfully:', result)
      }
      
      // Navigate to dashboard without logging out (keep user authenticated)
      console.log('🏠 Navigating to dashboard...')
      navigate('/dashboard')
    } catch (error) {
      console.error('❌ Error during case disconnect:', error)
      if (error instanceof Error) {
        console.error('Error type:', error.constructor.name)
        console.error('Error message:', error.message)
        if ('response' in error) {
          console.error('Response:', (error as any).response)
        }
      }
      // Still navigate to dashboard even if session save fails
      navigate('/dashboard')
    }
  }

  const handleResetVisibility = async () => {
    if (!currentCase) return
    
    if (confirm('⚠️ Reset visibility? This will clear all unlocked assets and emails for testing.')) {
      try {
        const result = await caseSessionApi.resetVisibility(currentCase)
        console.log('✅ Visibility reset:', result)
        alert(`Reset complete! Removed ${result.assetsRemoved} assets, ${result.emailsRemoved} emails, ${result.downloadsRemoved} downloads`)
        window.location.reload()
      } catch (error) {
        console.error('Failed to reset visibility:', error)
        alert('Failed to reset visibility')
      }
    }
  }

  return (
    <DesktopContainer>
      <SystemInfo>
        <div><span className="label">User:</span> {user?.firstName} {user?.lastName}</div>
        <div><span className="label">Unit:</span> {user?.department || 'Investigation Division'}</div>
        <div><span className="label">Badge:</span> #{user?.badgeNumber || '4729'}</div>
        <div><span className="label">{t('currentCase')}:</span> {currentCase || 'No Case'}</div>
        <div><span className="label">{t('currentLanguage')}:</span> {language.flag} {language.code}</div>
        <div><span className="label">Status:</span> Active</div>
        <button onClick={handleResetVisibility} style={{ marginTop: '0.5rem', padding: '0.5rem', background: '#ff4444', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
          🔄 Reset Visibility (DEV)
        </button>
      </SystemInfo>
      
      <DesktopArea>
        {windows.map(window => (
          <Window
            key={window.id}
            window={window}
            onClose={() => closeWindow(window.id)}
            onFocus={() => bringToFront(window.id)}
            onPositionChange={(position) => updateWindowPosition(window.id, position)}
            onSizeChange={(size) => updateWindowSize(window.id, size)}
            onMaximize={() => maximizeWindow(window.id)}
            onMinimize={() => minimizeWindow(window.id)}
          />
        ))}
      </DesktopArea>
      <Dock onOpenWindow={handleOpenWindow} onCaseDisconnect={handleCaseDisconnect} />
    </DesktopContainer>
  )
}

export default Desktop