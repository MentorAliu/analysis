# syntax=docker/dockerfile:1

FROM node:24.20.0-alpine3.24@sha256:e67514e5d0f6c46656005e1b693b2ec9d52e80b641307de684d4a015ba7a4eaf AS dependencies
WORKDIR /app

COPY --chown=node:node package.json package-lock.json .npmrc ./
RUN chown node:node /app
USER node
RUN npm ci

FROM dependencies AS development
COPY --chown=node:node . .

EXPOSE 5173
CMD ["npm", "run", "dev"]

FROM dependencies AS build
COPY --chown=node:node . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.31.4-alpine-slim@sha256:021f32b23e2bfc8610ccdec499b709625dcee1369884d7a51bd8a23a3accb301 AS production
COPY --chown=101:101 nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=101:101 /app/dist /usr/share/nginx/html

USER 101
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=5 \
  CMD ["/usr/bin/wget", "--spider", "-q", "http://127.0.0.1:8080/healthz"]
