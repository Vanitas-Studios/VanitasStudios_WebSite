import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
    plugins: [react()],
    build: {
        outDir: path.resolve(__dirname, '../../wwwroot/dist'), // Set the output directory to the wwwroot/dist folder
        emptyOutDir: true, // Clear the output directory before building
        rollupOptions: {
            input: './src/main.jsx', // Set the entry point for the build
        }
    }
})
