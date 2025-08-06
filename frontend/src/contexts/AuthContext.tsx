import React, { createContext, useState, useEffect } from 'react'
import type { User } from '../services/api'
import { authApi } from '../services/api'

interface AuthContextType {
  user: User | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

interface AuthProviderProps {
  children: React.ReactNode
}

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    // Check if user is already authenticated on app start
    const checkAuth = () => {
      const isAuth = authApi.isAuthenticated()
      
      // In development mode, if not authenticated, set a mock user
      if (!isAuth && import.meta.env.DEV) {
        const mockUser: User = {
          id: 'dev-user',
          email: 'detective@police.gov',
          firstName: 'John',
          lastName: 'Doe',
          department: 'Investigation Division',
          position: 'Detective',
          badgeNumber: '4729',
          isApproved: true
        }
        setUser(mockUser)
      } else if (!isAuth) {
        setUser(null)
      }
      setIsLoading(false)
    }

    checkAuth()
  }, [])

  const login = async (email: string, password: string) => {
    setIsLoading(true)
    try {
      const response = await authApi.login({ email, password })
      setUser(response.user)
    } finally {
      setIsLoading(false)
    }
  }

  const logout = () => {
    authApi.logout()
    setUser(null)
  }

  const value: AuthContextType = {
    user,
    isAuthenticated: !!user,
    isLoading,
    login,
    logout
  }

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  )
}

export default AuthContext