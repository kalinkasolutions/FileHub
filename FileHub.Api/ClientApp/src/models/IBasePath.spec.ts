import { describe, expect, it } from 'vitest';
import { IBasePath, grantLabel, isUngranted } from '@models/IBasePath';

function basePath(overrides: Partial<IBasePath> = {}): IBasePath {
  return {
    id: 'b',
    path: '/mnt/media',
    name: 'Media',
    createdAt: '2026-01-01T00:00:00Z',
    userCount: 0,
    groupCount: 0,
    ...overrides,
  };
}

describe('isUngranted', () => {
  it('needs both grant tables to be empty — a group grant is access too', () => {
    expect(isUngranted(basePath())).toBe(true);
    expect(isUngranted(basePath({ userCount: 1 }))).toBe(false);
    expect(isUngranted(basePath({ groupCount: 1 }))).toBe(false);
  });
});

describe('grantLabel', () => {
  it('says who can still see an ungranted path, which is admins rather than nobody', () => {
    expect(grantLabel(basePath())).toBe('Granted to nobody — only admins can see it');
  });

  it('keeps the two counts apart rather than adding them — a user can be in both', () => {
    expect(grantLabel(basePath({ userCount: 1, groupCount: 1 }))).toBe(
      'Granted to 1 user and 1 group, plus every admin',
    );
    expect(grantLabel(basePath({ userCount: 3, groupCount: 2 }))).toBe(
      'Granted to 3 users and 2 groups, plus every admin',
    );
  });
});
