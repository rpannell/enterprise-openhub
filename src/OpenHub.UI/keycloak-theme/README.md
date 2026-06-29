# OpenHub — Keycloak login theme

A CSS-only theme that re-skins Keycloak's own login page (the one Keycloak
serves before the React app ever loads) to match OpenHub's colors and logo.
No templates were rewritten — it extends Keycloak's default login theme and
only overrides the stylesheet and a couple of images, so it stays in sync
with Keycloak upgrades.

Dark mode follows the visitor's OS/browser setting automatically
(`prefers-color-scheme`) — there's no toggle on the login page itself, since
nobody's logged in yet to remember a preference for.

## Install it

**Docker:**

```bash
docker run -p 8080:8080 \
  -v $(pwd)/keycloak-theme/openhub:/opt/keycloak/themes/openhub \
  -e KEYCLOAK_ADMIN=admin -e KEYCLOAK_ADMIN_PASSWORD=admin \
  quay.io/keycloak/keycloak:latest start-dev
```

**Bare install:** copy the `openhub` folder into your Keycloak installation's
`themes/` directory, then restart Keycloak.

Then in the Keycloak Admin Console: **Realm Settings → Themes → Login
theme → openhub → Save.**

## What's in it

- `login/resources/css/styles.css` — colors, fonts, form styling, dark mode
- `login/resources/img/logo-light.svg`, `logo-dark.svg` — the OpenHub
  wordmark, swapped automatically based on the visitor's OS theme; replaces
  the realm display name text in the header via CSS background-image
- `login/resources/img/favicon.ico` — same favicon as the React app. Child
  theme resources override the parent's automatically, so dropping this
  file in is enough — no template changes needed.

## Editing it

While developing, disable theme caching so changes show up on refresh
without a restart:

```bash
bin/kc.sh start-dev --spi-theme--static-max-age=-1 --spi-theme--cache-themes=false --spi-theme--cache-templates=false
```

If a future Keycloak version changes its markup and a selector in
`styles.css` stops matching, open browser devtools on the login page to
find the new one.

## Matching fonts exactly (optional)

The app uses Space Grotesk (headings) + Inter (body) loaded from Google
Fonts. The login theme falls back to system fonts by default — some teams
prefer not to load third-party fonts on a login page. To match exactly,
uncomment the `@import` at the top of `styles.css`, or self-host the font
files in `login/resources/fonts/` and `@font-face` them instead.
