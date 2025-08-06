import Desktop from './components/Desktop'
import { WindowProvider } from './contexts/WindowContext'

function App() {
  return (
    <WindowProvider>
      <Desktop />
    </WindowProvider>
  )
}

export default App
