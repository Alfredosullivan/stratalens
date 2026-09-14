import { useEffect, useState } from 'react';
import { getCurrentUser } from '../services/authService';
import type { CurrentUser } from '../auth/types';

// A diferencia de useProjectGraph, acá 'error' NO es un mensaje para mostrar en pantalla:
// un 401 en /auth/me significa "no hay sesión", que RequireAuth traduce a un redirect a
// /login, nunca a un mensaje de error visible.
export type CurrentUserState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; user: CurrentUser };

export function useCurrentUser(): CurrentUserState {
  const [state, setState] = useState<CurrentUserState>({ status: 'loading' });

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });

    getCurrentUser()
      .then((user) => {
        if (!cancelled) setState({ status: 'ready', user });
      })
      .catch(() => {
        if (!cancelled) setState({ status: 'error' });
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
