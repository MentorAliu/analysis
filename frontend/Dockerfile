FROM node:24.20.0-bookworm-slim@sha256:ba849c60be29959425b8734d57b8b4b7d56f98edd9504c9af091d5281095a71e AS dependencies
WORKDIR /app
RUN chown node:node /app
COPY --chown=node:node frontend/package.json frontend/package-lock.json frontend/.npmrc ./
USER node
RUN test "$(node --version)" = "v24.20.0" && test "$(npm --version)" = "11.19.0" && npm ci

FROM dependencies AS development
COPY --chown=node:node frontend/ ./
EXPOSE 5173
HEALTHCHECK --interval=5s --timeout=3s --start-period=15s --retries=3 CMD ["node", "-e", "fetch('http://127.0.0.1:5173').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"]
CMD ["./node_modules/.bin/vite", "--host", "0.0.0.0"]

FROM dependencies AS build
COPY --chown=node:node frontend/ ./
RUN npm run lint && npm test && npm run build

FROM mcr.microsoft.com/playwright:v1.63.0-noble@sha256:eff16c30e6f3f4af0a03fa4b706120d5e9b0891c344a27d64559aff5900a4a27 AS e2e
WORKDIR /app
# Keep the same Node/npm toolchain as development, independent of the browser image.
COPY --from=dependencies /usr/local/bin/node /usr/local/bin/node
COPY --from=dependencies /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
COPY --from=build --chown=pwuser:pwuser /app/ ./
RUN chown pwuser:pwuser /app
USER pwuser
RUN test "$(node --version)" = "v24.20.0" && test "$(npm --version)" = "11.19.0"
CMD ["npm", "run", "test:e2e:run"]

FROM nginx:1.30.4-alpine@sha256:dc5069ad14f19660b141b21236140b91656bf89bbc3e2417c70ae650cd66104c AS production
COPY frontend/nginx.conf /etc/nginx/nginx.conf
COPY --from=build /app/dist/ /usr/share/nginx/html/
USER nginx
EXPOSE 5173
HEALTHCHECK --interval=5s --timeout=3s --start-period=10s --retries=3 CMD ["wget", "-q", "--spider", "http://127.0.0.1:5173/healthz"]
ENTRYPOINT ["nginx", "-g", "daemon off;"]
