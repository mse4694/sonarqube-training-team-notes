import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  // Produces .next/standalone for the small runtime image in the Dockerfile.
  output: 'standalone',
};

export default nextConfig;
