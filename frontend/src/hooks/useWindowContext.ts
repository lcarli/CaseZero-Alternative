import { useContext } from 'react'
import WindowContext from '../contexts/WindowContext'

export const useWindowContext = () => {
  const context = useContext(WindowContext)
  if (!context) {
    throw new Error('useWindowContext must be used within a WindowProvider')
  }
  return context
}