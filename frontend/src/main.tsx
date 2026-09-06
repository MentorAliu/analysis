import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ApplicationRoot } from '@/app/providers'
import { createApplication } from '@/app/application'
import './index.css'

const application = createApplication()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ApplicationRoot application={application} />
  </StrictMode>,
)
