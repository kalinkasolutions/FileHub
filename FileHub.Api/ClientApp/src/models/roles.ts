/**
 * Mirrors the backend's role constants. `Admin` unlocks the admin area and implies every other
 * role; `CreateShares` is what publishing a link needs. The server expands what `Admin` implies
 * before it answers `GET /api/auth/status`, so a plain `roles.includes(...)` here is enough — there
 * is no implication for the client to re-derive.
 */
export const Roles = {
  Admin: 'Admin',
  User: 'User',
  CreateShares: 'CreateShares',
} as const;
