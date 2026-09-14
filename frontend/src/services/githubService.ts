import { apiGet } from './api';
import type { Repository } from '../github/types';

export function listRepositories(): Promise<Repository[]> {
  return apiGet<Repository[]>('/api/v1/github/repositories');
}
