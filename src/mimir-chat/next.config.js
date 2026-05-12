const { i18n } = require('./next-i18next.config');

/** @type {import('next').NextConfig} */
const nextConfig = {
  // Enable standalone output for smaller Docker image and faster startup
  output: 'standalone',
  i18n,
  reactStrictMode: true,
  staticPageGenerationTimeout: 300,
  turbopack: {},

  webpack(config, { isServer, dev }) {
    config.experiments = {
      asyncWebAssembly: true,
      layers: true,
    };

    return config;
  },
};

module.exports = nextConfig;
