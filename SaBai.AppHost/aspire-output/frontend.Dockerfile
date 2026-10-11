FROM node:22-slim AS build
WORKDIR /app
ARG NPM_REGISTRY=https://registry.npmjs.org/
RUN npm install --global --registry "$NPM_REGISTRY" pnpm@10.30.1
COPY pnpm-lock.yaml pnpm-workspace.yaml ./
COPY SaBai.Web/package.json SaBai.Web/package.json
COPY SaBai.Sdk/package.json SaBai.Sdk/package.json
RUN pnpm install --frozen-lockfile
COPY SaBai.Web SaBai.Web
COPY SaBai.Sdk SaBai.Sdk
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
RUN pnpm --filter project-sabai... build

FROM node:22-slim AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
COPY --from=build --chown=node:node /app/SaBai.Web/.next/standalone ./
COPY --from=build --chown=node:node /app/SaBai.Web/.next/static ./SaBai.Web/.next/static
COPY --from=build --chown=node:node /app/SaBai.Web/public ./SaBai.Web/public
USER node
EXPOSE 3000
CMD ["node", "SaBai.Web/server.js"]
