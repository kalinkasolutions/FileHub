import { describe, expect, it } from 'vitest';
import {
  IGroup,
  basePathCountLabel,
  groupLossLabel,
  groupWarning,
  memberCountLabel,
  sortGroups,
} from '@models/IGroup';

function group(overrides: Partial<IGroup> = {}): IGroup {
  return {
    id: 'g',
    name: 'Family',
    memberCount: 2,
    basePathCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

describe('memberCountLabel', () => {
  it('counts members', () => {
    expect(memberCountLabel(group({ memberCount: 0 }))).toBe('0 members');
    expect(memberCountLabel(group({ memberCount: 1 }))).toBe('1 member');
    expect(memberCountLabel(group({ memberCount: 5 }))).toBe('5 members');
  });
});

describe('basePathCountLabel', () => {
  it('counts base paths', () => {
    expect(basePathCountLabel(group({ basePathCount: 0 }))).toBe('0 base paths');
    expect(basePathCountLabel(group({ basePathCount: 1 }))).toBe('1 base path');
  });
});

describe('groupWarning', () => {
  it('is silent about a group that has both halves', () => {
    expect(groupWarning(group())).toBe('');
  });

  it('says a group with no members grants to nobody', () => {
    expect(groupWarning(group({ memberCount: 0 }))).toBe(
      'Nobody is in it, so the base paths it grants reach no one.',
    );
  });

  it('says a group with no base paths grants nothing', () => {
    expect(groupWarning(group({ basePathCount: 0 }))).toBe(
      'It grants no base paths, so being a member gets you nothing.',
    );
  });

  it('says both when it is brand new', () => {
    expect(groupWarning(group({ memberCount: 0, basePathCount: 0 }))).toBe(
      'Nobody is in it and it grants nothing yet.',
    );
  });
});

describe('groupLossLabel', () => {
  it('does not confirm a loss of "0 base paths" — there is nothing to lose', () => {
    expect(groupLossLabel(group({ basePathCount: 0 }))).toBe(
      'It grants no base paths, so nothing is lost today — but they will not get whatever it is ' +
        'granted later.',
    );
  });

  it('says what is lost and that it takes the share links with it', () => {
    expect(groupLossLabel(group({ basePathCount: 2 }))).toBe(
      'They lose the 2 base paths it grants, unless they reach them another way, and every share ' +
        'link they made under them is deleted.',
    );
  });
});

describe('sortGroups', () => {
  it('puts the newest group first and leaves the input alone', () => {
    const groups = [
      group({ id: 'old', createdAt: '2026-01-01T00:00:00Z' }),
      group({ id: 'new', createdAt: '2026-06-01T00:00:00Z' }),
    ];

    expect(sortGroups(groups).map((g) => g.id)).toEqual(['new', 'old']);
    expect(groups.map((g) => g.id)).toEqual(['old', 'new']);
  });
});
