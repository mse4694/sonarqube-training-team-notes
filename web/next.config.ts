import type { NextConfig } from 'next';

// Calls to /api/* in the browser are proxied to the ASP.NET Core API (server-to-server),
// so the browser stays same-origin with the web app and cookies just work.
const API_INTERNAL = process.env.API_INTERNAL_URL ?? 'http://api:8080';

const nextConfig: NextConfig = {
  output: 'standalone',
  // Basic security headers for the web pages (the API sets its own).
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'X-Frame-Options', value: 'DENY' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
        ],
      },
    ];
  },
  async rewrites() {
    return [{ source: '/api/:path*', destination: `${API_INTERNAL}/api/:path*` }];
  },
};

export default nextConfig;
