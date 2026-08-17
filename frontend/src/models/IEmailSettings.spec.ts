import { describe, expect, it } from 'vitest';
import {
  emailIsConfigured,
  emptyEmailSettings,
  passwordPlaceholder,
  secureSocketOptions,
} from '@models/IEmailSettings';

describe('emailIsConfigured', () => {
  it('is false without a host — nothing is sent at all then', () => {
    expect(emailIsConfigured(emptyEmailSettings)).toBe(false);
    expect(emailIsConfigured({ ...emptyEmailSettings, smtpHost: '   ' })).toBe(false);
  });

  it('is true once a host is set', () => {
    expect(emailIsConfigured({ ...emptyEmailSettings, smtpHost: 'smtp.example.com' })).toBe(true);
  });
});

describe('passwordPlaceholder', () => {
  it('distinguishes a kept secret from no secret', () => {
    expect(passwordPlaceholder(true)).toBe('Unchanged');
    expect(passwordPlaceholder(false)).toBe('No password stored');
  });
});

describe('secureSocketOptions', () => {
  it('offers exactly the values the API accepts', () => {
    expect(secureSocketOptions.map((option) => option.value).sort()).toEqual([
      'Auto',
      'None',
      'SslOnConnect',
      'StartTls',
      'StartTlsWhenAvailable',
    ]);
  });

  it('defaults to one of them', () => {
    const values = secureSocketOptions.map((option) => option.value);
    expect(values).toContain(emptyEmailSettings.secureSocketOptions);
  });
});
