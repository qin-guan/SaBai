import { defineConfig } from 'kubb/config'
import { pluginTs } from '@kubb/plugin-ts'
import { pluginFetch } from '@kubb/plugin-fetch'
import { pluginReactQuery } from '@kubb/plugin-react-query'
import { pluginZod } from '@kubb/plugin-zod'
import { pluginFaker } from '@kubb/plugin-faker'
import { pluginMsw } from '@kubb/plugin-msw'
import { pluginMcp } from '@kubb/plugin-mcp'

export default defineConfig({
  input: 'http://localhost:5250/openapi/v1.json',
  output: {
    path: './src/gen',
    clean: true,
  },
  plugins: [
    pluginTs(),
    pluginFetch(),
    pluginReactQuery(),
    pluginZod(),
    pluginFaker(),
    pluginMsw(),
    pluginMcp(),
  ],
})
