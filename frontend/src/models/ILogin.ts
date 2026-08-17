export interface ILogin {
  /** Sign-in is by email address; a username is only a display name. */
  email: string;
  password: string;
}

/** Second step of a two-factor sign-in: an authenticator code, or one of the recovery codes. */
export interface ITwoFactorLogin {
  code: string;
  rememberMachine: boolean;
}

/**
 * Result of a successful password check — also the result of the two-factor step.
 * `requiresTwoFactor` means nothing is signed in yet and the code still has to be posted;
 * `mustChangePassword` means the session exists but only `/change-password` will let it through.
 */
export interface ILoginResult {
  requiresTwoFactor: boolean;
  mustChangePassword: boolean;
}
