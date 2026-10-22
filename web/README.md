# LeverDeck web (PWA)

The Angular PWA the phone or tablet opens. The agent (`../agent`) embeds the production build and serves it on
port 8765; see [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) for the folder layout and boundaries.

| Command | What it does |
|---------|--------------|
| `npm ci` | Installs the locked dependencies |
| `npm start` | Dev server on `http://localhost:4200` (no agent behind it, so the status stays "Reconnecting…") |
| `npm run lint` | ESLint, including the architecture boundaries (`eslint-plugin-boundaries`) |
| `npm test` | Unit tests once (Vitest through `ng test`) |
| `npm run build` | Production build into `dist/leverdeck/browser`, which the agent embeds |

`src/app/core/protocol/` is generated from `agent/src/LeverDeck.Protocol`; never edit it by hand.
