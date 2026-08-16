import { describe, expect, it } from 'vitest';
import { IAdminUser, sortUsers, toggleRole, userStatus } from '@models/IAdminUser';

function user(overrides: Partial<IAdminUser> = {}): IAdminUser {
  return {
    id: 'a',
    username: 'Ada',
    email: 'ada@example.com',
    emailConfirmed: true,
    roles: ['User'],
    mustChangePassword: false,
    isLockedOut: false,
    basePathCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

describe('userStatus', () => {
  it('is active for a confirmed, unlocked account', () => {
    expect(userStatus(user())).toBe('active');
  });

  it('is invited while the invitation has not been accepted', () => {
    expect(userStatus(user({ emailConfirmed: false }))).toBe('invited');
  });

  it('is disabled for a locked account', () => {
    expect(userStatus(user({ isLockedOut: true }))).toBe('disabled');
  });

  it('prefers disabled over invited — that is the one to undo first', () => {
    expect(userStatus(user({ isLockedOut: true, emailConfirmed: false }))).toBe('disabled');
  });
});

describe('toggleRole', () => {
  it('adds a role', () => {
    expect(toggleRole(['User'], 'Admin', true)).toEqual(['User', 'Admin']);
  });

  it('removes a role', () => {
    expect(toggleRole(['User', 'Admin'], 'Admin', false)).toEqual(['User']);
  });

  it('does not add a role twice', () => {
    expect(toggleRole(['User'], 'User', true)).toEqual(['User']);
  });

  it('always returns a new array so a signal sees the change', () => {
    const roles = ['User'];
    expect(toggleRole(roles, 'User', true)).not.toBe(roles);
    expect(toggleRole(roles, 'Admin', false)).not.toBe(roles);
  });
});

describe('sortUsers', () => {
  it('puts the newest account first and leaves the input alone', () => {
    const users = [
      user({ id: 'old', createdAt: '2026-01-01T00:00:00Z' }),
      user({ id: 'new', createdAt: '2026-06-01T00:00:00Z' }),
    ];

    expect(sortUsers(users).map((u) => u.id)).toEqual(['new', 'old']);
    expect(users.map((u) => u.id)).toEqual(['old', 'new']);
  });
});
