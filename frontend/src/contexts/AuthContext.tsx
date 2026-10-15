import React, { createContext, useState, useEffect } from 'react'
import type { User } from '../services/api'
import { ApiError, authApi, userStorage } from '../services/api'

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
    const checkAuth = async () => {
      const isAuth = authApi.isAuthenticated()
      
      if (isAuth) {
        const cachedUser = userStorage.get()
        try {
          const currentUser = await authApi.me()
          userStorage.set(currentUser)
          setUser(currentUser)
        } catch (error) {
          if (error instanceof ApiError && error.status === 401) {
            authApi.logout()
            setUser(null)
          } else {
            setUser(cachedUser)
          }
        }
      } else if (import.meta.env.DEV) {
        const mockUser: User = {
          id: 'dev-user',
          email: 'john.doe@fic-police.gov',
          personalEmail: 'john.doe@example.com',
          firstName: 'John',
          lastName: 'Doe',
          department: 'ColdCase',
          position: 'rook',
          badgeNumber: '4729',
          emailVerified: true,
          roles: ['PLAYER']
        }
        setUser(mockUser)
      } else {
        setUser(null)
      }
      setIsLoading(false)
    }

    void checkAuth()
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