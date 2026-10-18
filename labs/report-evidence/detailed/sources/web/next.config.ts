import type { NextConfig } from 'next';

// Calls to /api/* in the browser are proxied to the ASP.NET Core API (server-to-server),
// so the browser stays same-origin with the web app and cookies just work.
const API_INTERNAL = process.env.API_INTERNAL_URL ?? 'http://api:8080';

const nextConfig: NextConfig = {
  output: 'standalone',
  async rewrites() {
    return [{ source: '/api/:path*', destination: `${API_INTERNAL}/api/:path*` }];
  },
};

export default nextConfig;

