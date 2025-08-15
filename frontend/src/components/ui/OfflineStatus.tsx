import React, { useState, useEffect } from 'react'
import styled from 'styled-components'
import { useLanguage } from '../../hooks/useLanguageContext'

interface OfflineIndicatorProps {
  position?: 'top' | 'bottom'
}

const IndicatorContainer = styled.div<{ position: 'top' | 'bottom'; show: boolean }>`
  position: fixed;
  left: 50%;
  transform: translateX(-50%);
  z-index: 10000;
  padding: 0.75rem 1.5rem;
  border-radius: 6px;
  font-size: 0.9rem;
  font-weight: 500;
  color: white;
  text-align: center;
  transition: all 0.3s ease;
  min-width: 200px;
  
  ${props => props.position === 'top' ? 'top: 1rem;' : 'bottom: 1rem;'}
  
  ${props => props.show ? 'opacity: 1; transform: translateX(-50%) translateY(0);' : 
    props.position === 'top' ? 
      'opacity: 0; transform: translateX(-50%) translateY(-20px); pointer-events: none;' :
      'opacity: 0; transform: translateX(-50%) translateY(20px); pointer-events: none;'
  }
`

const OfflineIndicator = styled(IndicatorContainer)`
  background: rgba(231, 76, 60, 0.9);
  border: 1px solid rgba(231, 76, 60, 1);
  box-shadow: 0 4px 12px rgba(231, 76, 60, 0.3);
`

const OnlineIndicator = styled(IndicatorContainer)`
  background: rgba(46, 204, 113, 0.9);
  border: 1px solid rgba(46, 204, 113, 1);
  box-shadow: 0 4px 12px rgba(46, 204, 113, 0.3);
`

const StatusIcon = styled.span`
  margin-right: 0.5rem;
  font-size: 1.1rem;
`

const OfflineStatus: React.FC<OfflineIndicatorProps> = ({ position = 'top' }) => {
  const [isOnline, setIsOnline] = useState(navigator.onLine)
  const [showReconnected, setShowReconnected] = useState(false)
  const [wasOffline, setWasOffline] = useState(false)
  const { t } = useLanguage()

  useEffect(() => {
    const handleOnline = () => {
      setIsOnline(true)
      if (wasOffline) {
        setShowReconnected(true)
        setTimeout(() => setShowReconnected(false), 3000)
        setWasOffline(false)
      }
    }

    const handleOffline = () => {
      setIsOnline(false)
      setWasOffline(true)
      setShowReconnected(false)
    }

    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)

    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [wasOffline])

  if (showReconnected) {
    return (
      <OnlineIndicator position={position} show={true} role="status" aria-live="polite">
        <StatusIcon>✅</StatusIcon>
        {t('connectionRestored')}
      </OnlineIndicator>
    )
  }

  if (!isOnline) {
    return (
      <OfflineIndicator position={position} show={true} role="status" aria-live="polite">
        <StatusIcon>📡</StatusIcon>
        {t('offlineMessage')}
      </OfflineIndicator>
    )
  }

  return null
}

// Hook for checking online status
export const useOnlineStatus = () => {
  const [isOnline, setIsOnline] = useState(navigator.onLine)

  useEffect(() => {
    const handleOnline = () => setIsOnline(true)
    const handleOffline = () => setIsOnline(false)

    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)

    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [])

  return isOnline
}

// Service Worker registration utility
export const registerServiceWorker = async () => {
  if ('serviceWorker' in navigator) {
    try {
      const registration = await navigator.serviceWorker.register('/sw.js')
      console.log('Service Worker registered:', registration)
      return registration
    } catch (error) {
      console.error('Service Worker registration failed:', error)
      return null
    }
  }
  return null
}

export default OfflineStatus