export interface IForgotPassword {
  email: string;
}

/** Redeems the token from a "reset your password" mail. */
export interface IResetPassword {
  email: string;
  token: string;
  password: string;
}

/** The signed-in user changing their own password — also what clears a forced change. */
export interface IChangePassword {
  currentPassword: string;
  newPassword: string;
  /** Required: ChangePasswordDto validates that it matches, so the API rejects a body without it. */
  confirmPassword: string;
}
