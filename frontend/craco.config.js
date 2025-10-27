const path = require('path');

module.exports = {
  webpack: {
    configure: (webpackConfig) => {
      webpackConfig.resolve = {
        ...webpackConfig.resolve,
        alias: {
          ...webpackConfig.resolve.alias,
          'React': path.resolve(__dirname, 'node_modules/react'),
          'ReactDOM': path.resolve(__dirname, 'node_modules/react-dom'),
        },
      };
      return webpackConfig;
    },
  },
}; 