# syntax=docker/dockerfile:1

FROM node:26.8.1-alpine3.24@sha256:2d984a15c9b54fd0aeb608b8e0d0d83529eb34d2966db27a1fb4f1edc3d298a3 AS dependencies
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

FROM nginxinc/nginx-unprivileged:1.30.4-alpine-slim@sha256:bcf91d2c73ab64fa1c4ac7fbac5ac523057c8af7d553ab9251c7aef38c260979 AS production
COPY --chown=101:101 nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build --chown=101:101 /app/dist /usr/share/nginx/html

USER 101
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=5 \
  CMD ["/usr/bin/wget", "--spider", "-q", "http://127.0.0.1:8080/healthz"]
