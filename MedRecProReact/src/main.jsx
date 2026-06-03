import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// Shared masthead styling — single source of truth lives in the MedRecProStatic
// project and is also linked directly by the server-rendered pages (_Layout, Chat).
// Imported before index.css so the dashboard can adapt only the container width.
import '../../MedRecProStatic/wwwroot/css/masthead.css'
import './index.css'
import App from './App.jsx'

// The MVC island uses aeDashboardApp; the Vite dev page keeps root as a fallback.
const mountElement = document.getElementById('aeDashboardApp') ?? document.getElementById('root');

// Guard against a missing mount node so static-host mistakes fail loudly in development.
if (!mountElement) {
  throw new Error('AE dashboard mount element was not found.');
}

createRoot(mountElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
