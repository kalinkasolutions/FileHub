/**
 * The SMTP settings as `GET /api/admin/email/settings` returns them. There is no password field:
 * the stored secret is never readable, only replaceable — {@link IEmailSettings.hasPassword} is
 * all the form gets to say about it.
 */
export interface IEmailSettings {
  smtpHost: string;
  port: number;
  username: string;
  fromAddress: string;
  fromName: string;
  /** MailKit's `SecureSocketOptions` by name — one of {@link secureSocketOptions}. */
  secureSocketOptions: string;
  hasPassword: boolean;
  /**
   * True when the save just dropped the stored password because it moved: a different host, port
   * or transport, or the username removed. "Leave it empty to keep it" does not hold across a
   * move — the secret would otherwise be sent to somewhere new.
   */
  passwordCleared: boolean;
}

/**
 * `PUT /api/admin/email/settings`. An empty `password` keeps the stored one, which is what makes
 * editing the sender or the from-name possible without retyping a secret the screen cannot read
 * back — but only while the destination stays put. Change the host, the port, the transport or
 * the username and the stored password is dropped rather than sent somewhere new; the response's
 * `passwordCleared` says it happened.
 */
export interface IUpdateEmailSettings {
  smtpHost: string;
  port: number;
  username: string;
  password: string;
  fromAddress: string;
  fromName: string;
  secureSocketOptions: string;
}

/** `POST /api/admin/email/test`. */
export interface ISendTestEmail {
  recipient: string;
}

/**
 * The exact five values the API's `AllowedValues` accepts — a typo is a 400, not a silent fall
 * back to Auto, so the form picks from this list rather than taking free text.
 */
export const secureSocketOptions: readonly { value: string; label: string }[] = [
  { value: 'Auto', label: 'Auto — pick from the port' },
  { value: 'StartTls', label: 'STARTTLS — usually port 587' },
  { value: 'SslOnConnect', label: 'SSL on connect — usually port 465' },
  { value: 'StartTlsWhenAvailable', label: 'STARTTLS when available' },
  { value: 'None', label: 'None — no encryption' },
];

/** What the form shows before the first fetch answers, and for a server that has nothing stored. */
export const emptyEmailSettings: IEmailSettings = {
  smtpHost: '',
  port: 587,
  username: '',
  fromAddress: '',
  fromName: '',
  secureSocketOptions: 'Auto',
  hasPassword: false,
  passwordCleared: false,
};

/**
 * Without a host nothing is sent at all, and the one thing that breaks first is the invitation
 * mail — which is the only way an account can come into existence.
 */
export function emailIsConfigured(settings: IEmailSettings): boolean {
  return settings.smtpHost.trim().length > 0;
}

/**
 * What the password box should say it is doing. The distinction matters: an empty box over a
 * stored password means "keep it", the same empty box with nothing stored means "still none".
 */
export function passwordPlaceholder(hasPassword: boolean): string {
  return hasPassword ? 'Unchanged' : 'No password stored';
}
