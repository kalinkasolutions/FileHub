import { levelClass, logLevels } from '@models/ILogEntry';
import { describe, expect, it } from 'vitest';

describe('logLevels', () => {
  // Serilog's spelling, not Microsoft's: the column holds the Serilog name, so a picker offering
  // "Trace" or "Critical" would filter to nothing.
  it('is the six Serilog levels, least severe first', () => {
    expect(logLevels).toEqual(['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal']);
  });
});

describe('levelClass', () => {
  // Three answers, not six: routine, worth a look, and wrong.
  it('treats Fatal and Error the same', () => {
    expect(levelClass('Fatal')).toBe('error');
    expect(levelClass('Error')).toBe('error');
  });

  it('gives Warning its own treatment', () => {
    expect(levelClass('Warning')).toBe('warning');
  });

  it('gives Information its own treatment', () => {
    expect(levelClass('Information')).toBe('info');
  });

  it('groups Verbose and Debug as the quiet ones', () => {
    expect(levelClass('Verbose')).toBe('quiet');
    expect(levelClass('Debug')).toBe('quiet');
  });

  // A level the server one day adds must not fall through to the treatment reserved for errors.
  it('falls back to quiet for anything it does not know', () => {
    expect(levelClass('Something')).toBe('quiet');
  });
});
