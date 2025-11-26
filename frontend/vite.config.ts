import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tsconfigPaths from 'vite-tsconfig-paths'
import path from 'path'

// https://vitejs.dev/config/
export default defineConfig(({ command }) => ({
  base: command === 'build' ? (process.env.VITE_BASE_PATH || '/webapp/') : '/',
  plugins: [
    react(),
    tsconfigPaths() // This enables the path mapping from tsconfig.json
  ],
  resolve: {
    alias: {
      // Replicate the CRACO alias configuration
      'React': path.resolve(__dirname, 'node_modules/react'),
      'ReactDOM': path.resolve(__dirname, 'node_modules/react-dom'),
    },
  },
  server: {
    port: 3000,
    open: true
  },
  build: {
    outDir: 'build',
    sourcemap: true,
    chunkSizeWarningLimit: 1000, // Raise limit to 1MB for medical imaging libraries
    rollupOptions: {
      output: {
        manualChunks: {
          // Core React libraries
          'react-vendor': ['react', 'react-dom'],
          
          // UI libraries
          'ui-vendor': ['@fluentui/react', '@fluentui/react-charting'],
          
          // Medical imaging libraries (likely large)
          'medical-vendor': [
            'cornerstone-core',
            'cornerstone-tools', 
            'cornerstone-wado-image-loader',
            'cornerstone-math',
            'dcmjs',
            'dicom-parser'
          ],
          
          // Other large libraries
          'charts-vendor': ['recharts'],
          'utils-vendor': ['axios', 'gl-matrix', 'uuid']
        }
      }
    }
  },
  // Define environment variables for Vite
  define: {
    global: 'globalThis',
  },
  optimizeDeps: {
    include: ['react', 'react-dom']
  }
}))