/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  // Screen heading display: 'code' (default), 'title', or 'both'. See src/config/screens.ts.
  readonly VITE_SCREEN_LABEL_MODE?: string
}
