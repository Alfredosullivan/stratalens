// Cliente HTTP mínimo hacia el backend. Centraliza la URL base y el envío de la cookie
// de sesión (credentials:'include'), necesaria porque la auth es por cookie de GitHub
// OAuth. Un solo lugar que sabe "cómo hablamos con el API".

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

// Error tipado que conserva el status HTTP, para que la UI distinga 401/404/500.
// El campo se declara explícito (no como parameter property) porque el tsconfig usa
// erasableSyntaxOnly: solo permite sintaxis TS que se borra sin emitir código.
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  });

  if (!response.ok) {
    throw new ApiError(response.status, `GET ${path} → ${response.status}`);
  }

  return (await response.json()) as T;
}

export async function apiPost<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    credentials: 'include',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    throw new ApiError(response.status, `POST ${path} → ${response.status}`);
  }

  return (await response.json()) as T;
}
