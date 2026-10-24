import { defineConfig } from 'tsdown'

export default defineConfig({
    entry: ['./src/gen/{clients,handlers,hooks,mcp,mocks,types,zod}/index.ts'],
    dts: {
        sourcemap: true
    }
})