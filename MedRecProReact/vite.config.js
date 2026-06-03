import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';

// The dashboard is deployed as a single MVC-hosted island under /ae-dashboard/.
const dashboardBasePath = '/ae-dashboard/';

// The production bundle is committed into the MedRecProStatic web root.
const dashboardOutputPath = '../MedRecProStatic/wwwroot/ae-dashboard';

// https://vitejs.dev/config/
export default defineConfig({
    base: dashboardBasePath,
    plugins: [plugin()],
    server: {
        port: 50346,
        fs: {
            // Permit importing the shared masthead stylesheet from the sibling
            // MedRecProStatic project (one source of truth for the masthead style).
            allow: ['..'],
        },
    },
    build: {
        outDir: dashboardOutputPath,
        emptyOutDir: true,
        assetsDir: '',
        rollupOptions: {
            output: {
                entryFileNames: 'ae-dashboard.js',
                chunkFileNames: 'ae-dashboard-[name].js',
                assetFileNames: (assetInfo) => {
                    // Keep the MVC view stable by emitting a deterministic CSS filename.
                    if (assetInfo.name?.endsWith('.css')) {
                        return 'ae-dashboard.css';
                    }

                    // Preserve deterministic names for any future static assets.
                    return 'ae-dashboard-[name][extname]';
                },
            },
        },
    },
});
