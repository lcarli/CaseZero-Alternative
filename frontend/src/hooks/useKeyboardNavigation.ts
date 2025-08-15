import { useEffect } from 'react'
import type { RefObject } from 'react'

export interface KeyboardShortcut {
  key: string
  ctrlKey?: boolean
  shiftKey?: boolean
  altKey?: boolean
  metaKey?: boolean
  action: () => void
  description: string
  preventDefault?: boolean
}

// Hook for keyboard shortcuts
export const useKeyboardShortcuts = (shortcuts: KeyboardShortcut[], enabled: boolean = true) => {
  useEffect(() => {
    if (!enabled) return

    const handleKeyDown = (event: KeyboardEvent) => {
      const matchingShortcut = shortcuts.find(shortcut => {
        const keyMatch = shortcut.key.toLowerCase() === event.key.toLowerCase()
        const ctrlMatch = !!shortcut.ctrlKey === event.ctrlKey
        const shiftMatch = !!shortcut.shiftKey === event.shiftKey
        const altMatch = !!shortcut.altKey === event.altKey
        const metaMatch = !!shortcut.metaKey === event.metaKey

        return keyMatch && ctrlMatch && shiftMatch && altMatch && metaMatch
      })

      if (matchingShortcut) {
        if (matchingShortcut.preventDefault !== false) {
          event.preventDefault()
        }
        matchingShortcut.action()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [shortcuts, enabled])
}

// Hook for focus management
export const useFocusManagement = (containerRef: RefObject<HTMLElement>) => {
  const focusFirst = () => {
    if (!containerRef.current) return
    
    const focusableElements = containerRef.current.querySelectorAll(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )
    
    const firstElement = focusableElements[0] as HTMLElement
    if (firstElement) {
      firstElement.focus()
    }
  }

  const focusLast = () => {
    if (!containerRef.current) return
    
    const focusableElements = containerRef.current.querySelectorAll(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )
    
    const lastElement = focusableElements[focusableElements.length - 1] as HTMLElement
    if (lastElement) {
      lastElement.focus()
    }
  }

  const trapFocus = (event: KeyboardEvent) => {
    if (event.key !== 'Tab' || !containerRef.current) return

    const focusableElements = containerRef.current.querySelectorAll(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )

    const firstElement = focusableElements[0] as HTMLElement
    const lastElement = focusableElements[focusableElements.length - 1] as HTMLElement

    if (event.shiftKey) {
      if (document.activeElement === firstElement) {
        event.preventDefault()
        lastElement.focus()
      }
    } else {
      if (document.activeElement === lastElement) {
        event.preventDefault()
        firstElement.focus()
      }
    }
  }

  return { focusFirst, focusLast, trapFocus }
}

// Hook for arrow key navigation in lists
export const useArrowKeyNavigation = (
  containerRef: RefObject<HTMLElement>,
  itemSelector: string = '[role="listitem"], [role="option"], [data-navigable]',
  options: {
    loop?: boolean
    orientation?: 'horizontal' | 'vertical' | 'both'
    onSelect?: (element: HTMLElement, index: number) => void
  } = {}
) => {
  const { loop = true, orientation = 'vertical', onSelect } = options

  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const handleKeyDown = (event: KeyboardEvent) => {
      const items = Array.from(container.querySelectorAll(itemSelector)) as HTMLElement[]
      if (items.length === 0) return

      const currentIndex = items.findIndex(item => item === document.activeElement)
      let nextIndex = currentIndex

      switch (event.key) {
        case 'ArrowDown':
          if (orientation === 'vertical' || orientation === 'both') {
            event.preventDefault()
            nextIndex = currentIndex + 1
            if (nextIndex >= items.length) {
              nextIndex = loop ? 0 : items.length - 1
            }
          }
          break

        case 'ArrowUp':
          if (orientation === 'vertical' || orientation === 'both') {
            event.preventDefault()
            nextIndex = currentIndex - 1
            if (nextIndex < 0) {
              nextIndex = loop ? items.length - 1 : 0
            }
          }
          break

        case 'ArrowRight':
          if (orientation === 'horizontal' || orientation === 'both') {
            event.preventDefault()
            nextIndex = currentIndex + 1
            if (nextIndex >= items.length) {
              nextIndex = loop ? 0 : items.length - 1
            }
          }
          break

        case 'ArrowLeft':
          if (orientation === 'horizontal' || orientation === 'both') {
            event.preventDefault()
            nextIndex = currentIndex - 1
            if (nextIndex < 0) {
              nextIndex = loop ? items.length - 1 : 0
            }
          }
          break

        case 'Home':
          event.preventDefault()
          nextIndex = 0
          break

        case 'End':
          event.preventDefault()
          nextIndex = items.length - 1
          break

        case 'Enter':
        case ' ':
          if (currentIndex >= 0 && onSelect) {
            event.preventDefault()
            onSelect(items[currentIndex], currentIndex)
          }
          break

        default:
          return
      }

      if (nextIndex !== currentIndex && items[nextIndex]) {
        items[nextIndex].focus()
      }
    }

    container.addEventListener('keydown', handleKeyDown)
    return () => container.removeEventListener('keydown', handleKeyDown)
  }, [containerRef, itemSelector, loop, orientation, onSelect])
}

// Hook for escape key handling
export const useEscapeKey = (callback: () => void, enabled: boolean = true) => {
  useEffect(() => {
    if (!enabled) return

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        callback()
      }
    }

    document.addEventListener('keydown', handleEscape)
    return () => document.removeEventListener('keydown', handleEscape)
  }, [callback, enabled])
}

// Utility to make elements keyboard accessible
export const addKeyboardAccessibility = (element: HTMLElement) => {
  if (!element.hasAttribute('tabindex')) {
    element.setAttribute('tabindex', '0')
  }
  
  if (!element.hasAttribute('role')) {
    element.setAttribute('role', 'button')
  }

  const handleKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      element.click()
    }
  }

  element.addEventListener('keydown', handleKeyDown)
  
  return () => {
    element.removeEventListener('keydown', handleKeyDown)
  }
}