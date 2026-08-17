import { beforeEach, describe, expect, it } from 'vitest';
import { IFileEntry } from '@models/IFileEntry';
import { PathService } from '@services/path.service';

const disk = 'a1111111-1111-1111-1111-111111111111';

function directory(name: string, nextSegment: string, basePathId = disk): IFileEntry {
  return {
    id: basePathId,
    name,
    isDir: true,
    size: 0,
    nextSegment,
    isBasePath: nextSegment.length === 0,
    // Deliberately different every time, the way the server mints it.
    itemId: `${Math.random()}`,
  };
}

// The service has no dependencies, so it needs no TestBed — and it reads `history` in its
// constructor, which is why each test starts from a state with no trail in it.
describe('PathService', () => {
  beforeEach(() => {
    history.replaceState({}, '');
  });

  it('starts at the top, where the listing is the base paths', () => {
    const service = new PathService();

    expect(service.segments()).toEqual([]);
    expect(service.current()).toBeNull();
    expect(service.isAtTop()).toBe(true);
  });

  it('steps into a directory and mirrors the trail into history', () => {
    const service = new PathService();

    service.open(directory('media', ''));
    service.open(directory('music', 'music'));

    expect(service.segments().map((x) => x.name)).toEqual(['media', 'music']);
    expect(service.current()).toEqual({ basePathId: disk, name: 'music', path: 'music' });
    expect(history.state.pathSegments).toEqual(service.segments());
  });

  it('leaves the rest of the history state alone', () => {
    history.replaceState({ navigationId: 7 }, '');
    const service = new PathService();

    service.open(directory('media', ''));

    // The router keeps its own navigation id here; replacing the state instead of merging into it
    // leaves entries the router cannot restore.
    expect(history.state.navigationId).toBe(7);
  });

  it('restores the trail already on the history entry', () => {
    const segments = [{ basePathId: disk, name: 'media', path: '' }];
    history.replaceState({ pathSegments: segments }, '');

    expect(new PathService().segments()).toEqual(segments);
  });

  // Matched on (base path, path): the same directory carries a different itemId in every listing.
  it('ignores a directory that is already on the trail', () => {
    const service = new PathService();

    service.open(directory('media', ''));
    service.open(directory('media', ''));

    expect(service.segments()).toHaveLength(1);
  });

  it('tells two base paths with the same relative path apart', () => {
    const other = 'b2222222-2222-2222-2222-222222222222';
    const service = new PathService();

    service.open(directory('media', ''));
    service.open(directory('backups', '', other));

    expect(service.segments()).toHaveLength(2);
  });

  it('jumps back to a crumb and drops everything below it', () => {
    const service = new PathService();

    service.open(directory('media', ''));
    service.open(directory('music', 'music'));
    service.open(directory('live', 'music/live'));
    service.goTo(0);

    expect(service.segments().map((x) => x.name)).toEqual(['media']);
  });

  it('does nothing for the crumb you are standing on, or one that is not there', () => {
    const service = new PathService();

    service.open(directory('media', ''));
    service.open(directory('music', 'music'));

    service.goTo(1);
    service.goTo(9);
    service.goTo(-1);

    expect(service.segments()).toHaveLength(2);
  });

  it('goes one level up, and stops at the top', () => {
    const service = new PathService();

    service.open(directory('media', ''));
    service.open(directory('music', 'music'));

    service.up();
    expect(service.segments().map((x) => x.name)).toEqual(['media']);

    service.up();
    service.up();
    expect(service.isAtTop()).toBe(true);
  });

  it('goes all the way home', () => {
    const service = new PathService();

    service.open(directory('media', ''));
    service.open(directory('music', 'music'));
    service.goHome();

    expect(service.segments()).toEqual([]);
    expect(history.state.pathSegments).toEqual([]);
  });
});
