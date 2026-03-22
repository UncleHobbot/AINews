import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'

export interface ScanProgress {
  scanRunId: number
  status: string
  currentSource: string
  sourcesCompleted: number
  totalSources: number
  postsFetched: number
  newsItemsCreated: number
  message: string | null
}

export function useSignalR() {
  const [progress, setProgress] = useState<ScanProgress | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const connectionRef = useRef<signalR.HubConnection | null>(null)

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/scan')
      .withAutomaticReconnect()
      .build()

    connection.on('ScanProgress', (data: ScanProgress) => {
      setProgress(data)
    })

    connection.onreconnected(() => setIsConnected(true))
    connection.onclose(() => setIsConnected(false))

    connection
      .start()
      .then(() => setIsConnected(true))
      .catch(() => setIsConnected(false))

    connectionRef.current = connection
    return () => { connection.stop() }
  }, [])

  return { progress, isConnected }
}
