import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

/******** IMPORTANT : npm --prefix "..\MedRecProReact" run build *********/

// The masthead is server-rendered by the shared _Masthead.cshtml partial and
// styled by masthead.css, which the MVC host pages link directly (including the
// dashboard host view, Views/AdverseEventDashboard/Index.cshtml). React no
// longer renders or styles the masthead, so this island ships only the
// dashboard body styles.
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
