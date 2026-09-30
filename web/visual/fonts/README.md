# Vendored Inter for the visual gate

`inter-latin-wght-normal.woff2` is Inter (variable weight 100–900, normal style, Latin subset)
from the `@fontsource-variable/inter` npm package, version 5.3.0 (`files/inter-latin-wght-normal.woff2`),
SHA-256 `3100e775e8616cd2611beecfa23a4263d7037586789b43f035236a2e6fbd4c62`.
`OFL.txt` is that package's `LICENSE` (SIL Open Font License 1.1).

Only `../home.visual.spec.ts` uses it: it answers the app's Google Fonts requests with this file,
so screenshots never depend on the network or on Google changing the files it serves.
Production still loads Inter from Google Fonts (`app/root.tsx`). Replacing this file changes
text rendering, so regenerate the baselines with `npm run visual:update` in the same commit.
