import * as signalR from '@microsoft/signalr'

class ForensicsSignalRService {
  private connection: signalR.HubConnection | null = null
  private listeners: ((data: any) => void)[] = []

  async connect(token: string): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/hubs/forensics', {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build()

    this.connection.on('ForensicCompleted', (data) => {
      console.log('Forensic completed notification:', data)
      this.listeners.forEach(listener => listener(data))
    })

    try {
      await this.connection.start()
      console.log('SignalR connected to Forensics Hub')
    } catch (error) {
      console.error('Error connecting to SignalR:', error)
      throw error
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop()
      this.connection = null
    }
  }

  onForensicCompleted(callback: (data: any) => void): () => void {
    this.listeners.push(callback)
    return () => {
      this.listeners = this.listeners.filter(l => l !== callback)
    }
  }
}

export const forensicsSignalR = new ForensicsSignalRService()
