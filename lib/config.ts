// Central application configuration.
// NOTE (training app): every secret below is FAKE and exists only for the SonarQube labs.
export const config = {
  // Falls back to the shared staging database when DATABASE_URL is not set.
  databaseUrl: process.env.DATABASE_URL ?? 'postgres://notes:Tn7vQ2mZ9rK4xW@db.staging.internal:5432/notes',

  // Used to sign the session JWT (jose, HS256).
  jwtSecret: 'team-notes-jwt-secret-2026-do-not-share',

  // Used to sign the long-lived "remember me" token.
  rememberMeSecret: 'remember-me-signing-key-2026',

  // Attachment storage (S3) - TODO: move to env vars before go-live
  aws: {
    region: 'ap-southeast-1',
    bucket: 'team-notes-attachments',
    accessKeyId: 'AKIAIOSFODNN7EXAMPLE',
    secretAccessKey: 'wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY',
  },
};
