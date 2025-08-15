import { useLanguage } from '../hooks/useLanguageContext'
import type { Translations } from '../types/i18n'

// Error mapping utility for better error messages
export const getErrorMessage = (error: unknown, t: (key: keyof Translations) => string): string => {
  if (error instanceof Error) {
    const message = error.message.toLowerCase()
    
    // Network errors
    if (message.includes('network') || message.includes('fetch')) {
      return t('networkError')
    }
    
    // Server errors
    if (message.includes('500') || message.includes('internal server')) {
      return t('serverError')
    }
    
    // Authentication errors
    if (message.includes('401') || message.includes('unauthorized')) {
      return t('unauthorizedError')
    }
    
    // Permission errors
    if (message.includes('403') || message.includes('forbidden')) {
      return t('forbiddenError')
    }
    
    // Not found errors
    if (message.includes('404') || message.includes('not found')) {
      return t('notFoundError')
    }
    
    // Validation errors
    if (message.includes('400') || message.includes('validation')) {
      return t('validationError')
    }
    
    // Timeout errors
    if (message.includes('timeout')) {
      return t('timeoutError')
    }
    
    // Return original message if no mapping found
    return error.message
  }
  
  // Check if it's an API response object
  if (typeof error === 'object' && error !== null) {
    const errorObj = error as Record<string, unknown>
    
    if (errorObj.offline) {
      return t('offlineMessage')
    }
    
    if (typeof errorObj.message === 'string') {
      return getErrorMessage(new Error(errorObj.message), t)
    }
    
    if (typeof errorObj.error === 'string') {
      return getErrorMessage(new Error(errorObj.error), t)
    }
  }
  
  // Fallback to generic error
  return t('genericError')
}

// Enhanced fetch wrapper with error handling
export const apiRequest = async <T>(
  url: string, 
  options: RequestInit = {},
  t: (key: keyof Translations) => string
): Promise<T> => {
  const controller = new AbortController()
  const timeoutId = setTimeout(() => controller.abort(), 30000) // 30 second timeout
  
  try {
    const response = await fetch(url, {
      ...options,
      signal: controller.signal,
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
    })
    
    clearTimeout(timeoutId)
    
    if (!response.ok) {
      if (response.status === 401) {
        throw new Error(t('unauthorizedError'))
      }
      if (response.status === 403) {
        throw new Error(t('forbiddenError'))
      }
      if (response.status === 404) {
        throw new Error(t('notFoundError'))
      }
      if (response.status >= 500) {
        throw new Error(t('serverError'))
      }
      if (response.status === 400) {
        throw new Error(t('validationError'))
      }
      
      throw new Error(`HTTP ${response.status}: ${response.statusText}`)
    }
    
    const data = await response.json()
    return data
  } catch (error) {
    clearTimeout(timeoutId)
    
    if (error instanceof Error) {
      if (error.name === 'AbortError') {
        throw new Error(t('timeoutError'))
      }
      if (error.message.includes('NetworkError') || error.message.includes('Failed to fetch')) {
        throw new Error(t('networkError'))
      }
    }
    
    throw error
  }
}

// Hook for handling API errors with toast notifications (future enhancement)
export const useApiError = () => {
  const { t } = useLanguage()
  
  const handleError = (error: unknown) => {
    const message = getErrorMessage(error, t)
    console.error('API Error:', message)
    // Here you could add toast notifications or other error handling
    return message
  }
  
  return { handleError, getErrorMessage: (error: unknown) => getErrorMessage(error, t) }
}