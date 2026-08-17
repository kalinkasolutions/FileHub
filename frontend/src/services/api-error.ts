import { HttpErrorResponse } from '@angular/common/http';

/** ProblemDetails / ValidationProblemDetails, the two shapes the API answers failures with. */
interface ProblemDetails {
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * A message worth showing the user for a failed API call. Business errors arrive as ProblemDetails
 * (`detail`); DTO shape errors as ValidationProblemDetails, where the messages sit under `errors`
 * keyed by field — so read both before falling back.
 */
export function apiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const problem = error.error as ProblemDetails | null;
  if (problem?.detail) {
    return problem.detail;
  }

  const firstField = Object.values(problem?.errors ?? {})[0];
  return firstField?.[0] ?? fallback;
}
