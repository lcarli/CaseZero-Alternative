import React, { Component } from 'react'
import type { ReactNode } from 'react'
import styled from 'styled-components'
import { useLanguage } from '../../hooks/useLanguageContext'

interface ErrorBoundaryState {
  hasError: boolean
  error?: Error
}

interface ErrorBoundaryProps {
  children: ReactNode
  fallback?: ReactNode
}

const ErrorContainer = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 2rem;
  margin: 2rem;
  background: rgba(231, 76, 60, 0.1);
  border: 1px solid rgba(231, 76, 60, 0.3);
  border-radius: 8px;
  color: rgba(231, 76, 60, 0.9);
  text-align: center;
`

const ErrorIcon = styled.div`
  font-size: 3rem;
  margin-bottom: 1rem;
`

const ErrorTitle = styled.h2`
  margin: 0 0 1rem 0;
  font-size: 1.5rem;
  color: rgba(231, 76, 60, 1);
`

const ErrorMessage = styled.p`
  margin: 0 0 1.5rem 0;
  font-size: 1rem;
  line-height: 1.5;
`

const RetryButton = styled.button`
  background: rgba(231, 76, 60, 0.8);
  color: white;
  border: none;
  padding: 0.75rem 1.5rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 1rem;
  transition: all 0.2s ease;

  &:hover {
    background: rgba(231, 76, 60, 1);
    transform: translateY(-1px);
  }

  &:focus {
    outline: 2px solid rgba(231, 76, 60, 0.5);
    outline-offset: 2px;
  }
`

// Error fallback component
const ErrorFallback: React.FC<{ error?: Error; onRetry: () => void }> = ({ error, onRetry }) => {
  const { t } = useLanguage()

  return (
    <ErrorContainer role="alert" aria-live="assertive">
      <ErrorIcon>⚠️</ErrorIcon>
      <ErrorTitle>{t('errorOccurred')}</ErrorTitle>
      <ErrorMessage>
        {error?.message || t('genericError')}
      </ErrorMessage>
      <RetryButton 
        onClick={onRetry}
        aria-label={t('tryAgain')}
      >
        {t('tryAgain')}
      </RetryButton>
    </ErrorContainer>
  )
}

class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false }
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    console.error('ErrorBoundary caught an error:', error, errorInfo)
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: undefined })
  }

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback
      }
      
      return (
        <ErrorFallback 
          error={this.state.error} 
          onRetry={this.handleRetry}
        />
      )
    }

    return this.props.children
  }
}

export default ErrorBoundary
export { ErrorFallback }