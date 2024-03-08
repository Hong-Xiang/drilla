# drilla

Drilla engine for HPC and visualization

## develop

requirements:

- [Caddy](https://caddyserver.com/docs/) for reverse proxy to combine React SPA and ASP .NET Core server.
- [Node.js](https://nodejs.org/en) and [pnpm](https://pnpm.io/)
- [dotnet 8.0+](https://dotnet.microsoft.com/en-us/download)
- certificate files to serve https server, which is required for WebXR. You can use `mkcert` to generate it.


### run dev environment

#### onetime

* Add `DRILLA_SITE_ADDRESS` environment variable, this will be read in caddy file

* Use `mkcert` to generate self-assigned certificates `cert.pem` and `key.pem`, put them into `src/caddy/.cert` directory

#### dev loop

* Open `projects/Drilla.sln`, run `server` project to start a backend server

* Enter `src/client` directory, run `pnpm run dev` to start a dev server to provide frontend code

* Enter `src/caddy` directory, run `caddy run` to start a reverse proxy to combine the above services

* Make modifications, both server and client supports hot module reload,
simply save may work in many cases,
when hot module reload does not work,
restart corresponding service.

## Bi-directional camera stream

Start all services, open pages in 