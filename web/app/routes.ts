import { type RouteConfig, index, layout, route } from "@react-router/dev/routes";

export default [
  layout("routes/protected.tsx", [
    index("routes/home.tsx"),
    route("maps", "routes/maps.tsx"),
    route("maps/:id", "routes/saved-map.tsx"),
  ]),
  route("login", "routes/login.tsx"),
  route("register", "routes/register.tsx"),
] satisfies RouteConfig;
