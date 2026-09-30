// Fixed by the AppHost: Vite apps on 5173/5174, Mailpit HTTP on 8025.
export const clientUrl = 'http://localhost:5173';
export const studioUrl = 'http://localhost:5174';
export const mailpitUrl = 'http://localhost:8025';

// Seeded as an admin by the AppHost in run mode (Identity:AdminEmails:99).
export const adminEmail = 'admin@bayubai.local';

export const adminStatePath = '.auth/admin.json';

export function uniqueEmail(role: string): string {
  return `${role}-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.test`;
}
