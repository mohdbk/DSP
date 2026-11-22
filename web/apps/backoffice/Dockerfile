# Build context: repository root
FROM node:22-alpine AS build
WORKDIR /web
COPY web/ ./
RUN npm install --no-audit --no-fund && npm run build -w @dsp/backoffice

FROM nginx:1.27-alpine
ENV PORT=8080 GATEWAY_URL=http://gateway.railway.internal:8080 NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1
COPY web/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /web/apps/backoffice/dist /usr/share/nginx/html
EXPOSE 8080
