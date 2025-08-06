import { useContext } from 'react'
import CaseContext from '../contexts/CaseContext'

export const useCase = () => {
  const context = useContext(CaseContext)
  if (!context) {
    throw new Error('useCase must be used within a CaseProvider')
  }
  return context
}