import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { apiErrorMessage } from '@services/api-error';

function problem(body: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status: 400, error: body });
}

describe('apiErrorMessage', () => {
  it('reads the ProblemDetails detail', () => {
    expect(apiErrorMessage(problem({ detail: 'Invalid credentials' }), 'fallback')).toBe(
      'Invalid credentials',
    );
  });

  it('reads the first message of a ValidationProblemDetails', () => {
    const body = { errors: { Password: ['Password is too short', 'and too simple'] } };
    expect(apiErrorMessage(problem(body), 'fallback')).toBe('Password is too short');
  });

  it('prefers detail over errors', () => {
    const body = { detail: 'Nope', errors: { Email: ['Required'] } };
    expect(apiErrorMessage(problem(body), 'fallback')).toBe('Nope');
  });

  it('falls back for an empty body, an unknown shape and a non-HTTP failure', () => {
    expect(apiErrorMessage(problem(null), 'fallback')).toBe('fallback');
    expect(apiErrorMessage(problem({ errors: {} }), 'fallback')).toBe('fallback');
    expect(apiErrorMessage(new Error('boom'), 'fallback')).toBe('fallback');
  });
});
