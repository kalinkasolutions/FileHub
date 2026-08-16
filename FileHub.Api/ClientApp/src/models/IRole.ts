/**
 * One of the fixed roles with how many accounts hold it. Roles are seeded by the backend and are
 * never created or deleted through the API, so this list is read-only: it exists to tick boxes on
 * the user form and to show, at a glance, that somebody still holds `Admin`.
 */
export interface IRole {
  name: string;
  userCount: number;
}
