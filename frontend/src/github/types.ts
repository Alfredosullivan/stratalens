// Refleja RepositorySummaryDto (T25): repos que el usuario POSEE (affiliation=owner).
export interface Repository {
  owner: string;
  name: string;
  defaultBranch: string;
  isPrivate: boolean;
}
