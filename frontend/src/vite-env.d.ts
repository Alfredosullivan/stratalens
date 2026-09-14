/// <reference types="vite/client" />

// Tipado de las variables de entorno de Vite que usamos (deben empezar por VITE_).
interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
