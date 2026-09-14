// Refleja la respuesta de GET /api/v1/auth/me (T6/AuthController).
export interface CurrentUser {
  userId: string;
  login: string | null;
}
