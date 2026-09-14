// Refleja ProjectDto (T28): RepositoryOwner/RepositoryName nullable — presentes si el
// proyecto viene de GitHub (T26), null si es manual (T24: CreateManual).
export interface ProjectSummary {
  id: string;
  name: string;
  createdAt: string;
  repositoryOwner: string | null;
  repositoryName: string | null;
}

// Refleja CreateProjectResponse (T1/T26). ingestKey solo existe en este instante: no se
// puede volver a pedir después (el backend solo guarda su hash).
export interface CreatedProject {
  id: string;
  name: string;
  createdAt: string;
  ingestKey: string;
}
