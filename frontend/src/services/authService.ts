import { apiGet } from './api';
import type { CurrentUser } from '../auth/types';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

// Navegación real (no fetch): el Challenge de OAuth necesita llevarse el navegador a
// GitHub. returnUrl apunta al FRONTEND — sin esto, el backend redirige de vuelta a sí
// mismo tras el login (su default es "/"), dejando al usuario en el puerto de la Api.
export function loginUrl(): string {
  const returnUrl = encodeURIComponent(`${window.location.origin}/`);
  return `${BASE_URL}/api/v1/auth/github/login?returnUrl=${returnUrl}`;
}

export function getCurrentUser(): Promise<CurrentUser> {
  return apiGet<CurrentUser>('/api/v1/auth/me');
}
