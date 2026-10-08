import type { Metadata } from 'next';
import type { ReactNode } from 'react';

export const metadata: Metadata = {
  title: 'Team Notes',
  description: 'Deliberately vulnerable notes app for SonarQube developer training',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 760, margin: '2rem auto', padding: '0 1rem' }}>
        <header style={{ borderBottom: '1px solid #ddd', marginBottom: '1.5rem' }}>
          <h1 style={{ fontSize: '1.4rem' }}>Team Notes</h1>
        </header>
        {children}
      </body>
    </html>
  );
}
