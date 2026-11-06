FROM node:24-slim AS build
WORKDIR /app
ARG NPM_REGISTRY=https://registry.npmjs.org/
RUN npm install --global --registry "$NPM_REGISTRY" pnpm@latest
COPY pnpm-lock.yaml pnpm-workspace.yaml ./
COPY SaBai.Web.Nuxt/package.json SaBai.Web.Nuxt/package.json
COPY SaBai.Sdk/package.json SaBai.Sdk/package.json
RUN --mount=type=cache,target=/pnpm/store pnpm install --frozen-lockfile
COPY SaBai.Web.Nuxt SaBai.Web.Nuxt
COPY SaBai.Sdk SaBai.Sdk
RUN pnpm --filter @sabai/sdk... build
RUN pnpm --filter project-sabai-web-nuxt... build

FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
COPY --from=build --chown=node:node /app/SaBai.Web.Nuxt/.output .
USER node
ENTRYPOINT ["node","SaBai.Web.Nuxt.output/server/index.mjs"]
