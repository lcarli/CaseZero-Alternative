import React from 'react'
import styled, { keyframes } from 'styled-components'
import { useLanguage } from '../../hooks/useLanguageContext'

interface LoadingSpinnerProps {
  size?: 'small' | 'medium' | 'large'
  message?: string
  overlay?: boolean
}

const spin = keyframes`
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
`

const LoadingContainer = styled.div<{ overlay?: boolean }>`
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  padding: 2rem;
  
  ${props => props.overlay && `
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.7);
    z-index: 9999;
    backdrop-filter: blur(4px);
  `}
`

const Spinner = styled.div<{ size: 'small' | 'medium' | 'large' }>`
  border: 3px solid rgba(52, 152, 219, 0.3);
  border-top: 3px solid rgba(52, 152, 219, 1);
  border-radius: 50%;
  animation: ${spin} 1s linear infinite;
  
  ${props => {
    switch (props.size) {
      case 'small':
        return 'width: 20px; height: 20px; border-width: 2px;'
      case 'medium':
        return 'width: 40px; height: 40px; border-width: 3px;'
      case 'large':
        return 'width: 60px; height: 60px; border-width: 4px;'
      default:
        return 'width: 40px; height: 40px; border-width: 3px;'
    }
  }}
`

const LoadingMessage = styled.p`
  color: rgba(52, 152, 219, 0.9);
  font-size: 1rem;
  margin: 0;
  text-align: center;
  font-weight: 500;
`

const LoadingSpinner: React.FC<LoadingSpinnerProps> = ({ 
  size = 'medium', 
  message, 
  overlay = false 
}) => {
  const { t } = useLanguage()
  
  return (
    <LoadingContainer overlay={overlay} role="status" aria-live="polite">
      <Spinner size={size} aria-hidden="true" />
      <LoadingMessage>
        {message || t('loading')}
      </LoadingMessage>
    </LoadingContainer>
  )
}

// Skeleton loading components for content
const SkeletonBase = styled.div`
  background: linear-gradient(90deg, rgba(255,255,255,0.1) 25%, rgba(255,255,255,0.2) 50%, rgba(255,255,255,0.1) 75%);
  background-size: 200% 100%;
  animation: loading 1.5s infinite;
  border-radius: 4px;

  @keyframes loading {
    0% { background-position: 200% 0; }
    100% { background-position: -200% 0; }
  }
`

const SkeletonText = styled(SkeletonBase)<{ width?: string; height?: string }>`
  height: ${props => props.height || '1rem'};
  width: ${props => props.width || '100%'};
  margin: 0.25rem 0;
`

const SkeletonCard = styled(SkeletonBase)`
  height: 120px;
  width: 100%;
  margin: 0.5rem 0;
`

const SkeletonAvatar = styled(SkeletonBase)`
  width: 40px;
  height: 40px;
  border-radius: 50%;
`

interface SkeletonListProps {
  count?: number
  type?: 'text' | 'card' | 'table'
}

const SkeletonList: React.FC<SkeletonListProps> = ({ count = 3, type = 'text' }) => {
  const items = Array.from({ length: count }, (_, index) => {
    switch (type) {
      case 'card':
        return <SkeletonCard key={index} />
      case 'table':
        return (
          <div key={index} style={{ display: 'flex', gap: '1rem', alignItems: 'center', margin: '0.5rem 0' }}>
            <SkeletonAvatar />
            <div style={{ flex: 1 }}>
              <SkeletonText width="70%" />
              <SkeletonText width="50%" height="0.8rem" />
            </div>
          </div>
        )
      default:
        return <SkeletonText key={index} width={`${Math.random() * 30 + 60}%`} />
    }
  })

  return <div>{items}</div>
}

// Button loading state
interface LoadingButtonProps {
  loading?: boolean
  children: React.ReactNode
  onClick?: () => void
  disabled?: boolean
  type?: 'button' | 'submit' | 'reset'
  className?: string
}

const ButtonContainer = styled.button<{ loading?: boolean }>`
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 4px;
  background: rgba(52, 152, 219, 0.8);
  color: white;
  font-size: 1rem;
  cursor: pointer;
  transition: all 0.2s ease;
  
  &:hover:not(:disabled) {
    background: rgba(52, 152, 219, 1);
    transform: translateY(-1px);
  }
  
  &:focus {
    outline: 2px solid rgba(52, 152, 219, 0.5);
    outline-offset: 2px;
  }
  
  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
    transform: none;
  }
  
  ${props => props.loading && `
    pointer-events: none;
  `}
`

const LoadingButton: React.FC<LoadingButtonProps> = ({
  loading = false,
  children,
  onClick,
  disabled = false,
  type = 'button',
  className
}) => {
  return (
    <ButtonContainer
      type={type}
      onClick={onClick}
      disabled={disabled || loading}
      loading={loading}
      className={className}
      aria-busy={loading}
    >
      {loading && <Spinner size="small" />}
      {children}
    </ButtonContainer>
  )
}

export { LoadingSpinner, SkeletonText, SkeletonCard, SkeletonList, LoadingButton }
export default LoadingSpinner