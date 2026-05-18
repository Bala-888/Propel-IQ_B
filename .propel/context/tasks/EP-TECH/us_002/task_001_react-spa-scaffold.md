# Task - TASK_001

## Requirement Reference
- **User Story:** us_002
- **Story Location:** .propel/context/tasks/EP-TECH/us_002/us_002.md
- **Acceptance Criteria:**
  - AC-001: `npm run build` in `frontend/` completes with zero TypeScript errors and zero ESLint errors; `dist/` contains `index.html` and hashed static asset files
  - AC-005: Body text renders with font family `IBM Plex Sans` when the SPA runs in development mode (`npm start`)
- **Edge Cases:**
  - N/A — edge cases for this story concern routing/auth logic and are addressed in task_002

---

## Design References
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## AI References
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References
| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version | Justification |
|-------|------------|---------|---------------|
| Frontend | React | 18.x | TR-001 (React 18 mandated), NFR-008 (superior data grid ecosystem for clinical views) |
| Frontend | TypeScript | 5.x (latest stable) | TR-001 (TypeScript mandated), NFR-010 (free OSS) |
| Frontend | Vite | 5.x (latest stable) | NFR-010 (free OSS, CRA deprecated), NFR-008 (fast build and HMR for developer productivity) |
| Frontend | ESLint | 8.x (latest stable) | NFR-010 (free OSS), code quality enforcement |
| Frontend | IBM Plex Sans | via Google Fonts CDN | AC-005 (mandated font for SPA body text) |

---

## Task Overview

Bootstrap the `frontend/` React 18 TypeScript SPA using Vite. Configure `tsconfig.json` in strict mode, wire ESLint with `@typescript-eslint` and React hooks rules, set up `package.json` scripts (`dev`, `build`, `lint`, `preview`), and inject the IBM Plex Sans Google Fonts `<link>` into `index.html` with matching `font-family` in `src/index.css`. The output of this task is a clean, production-buildable scaffold — zero TS errors and zero ESLint errors — on which task_002 routing and auth work will be layered.

---

## Dependent Tasks
- task_001 (us_001) — `docker-compose.yml` nginx service must exist to serve the built SPA; this task creates the source to be built and served

---

## Impacted Components
- `frontend/` — new top-level directory for the React SPA
- `frontend/package.json` — project dependencies and npm scripts
- `frontend/vite.config.ts` — Vite build configuration
- `frontend/tsconfig.json` — TypeScript strict-mode compiler options
- `frontend/index.html` — SPA entry HTML with IBM Plex Sans `<link>` tag
- `frontend/src/index.css` — Global CSS with `font-family: 'IBM Plex Sans', sans-serif`
- `frontend/src/main.tsx` — React DOM render entry point
- `frontend/src/App.tsx` — Root app component (placeholder for routing in task_002)
- `frontend/.eslintrc.cjs` — ESLint configuration with `@typescript-eslint` and `react-hooks` plugins

---

## Implementation Plan
1. Initialise Vite React TypeScript project: `npm create vite@latest frontend -- --template react-ts`; remove boilerplate (`App.css`, `assets/react.svg`, default counter component)
2. Configure `tsconfig.json` with `"strict": true`, `"noUnusedLocals": true`, `"noUnusedParameters": true`, and `"noImplicitReturns": true` under `compilerOptions`
3. Add `.eslintrc.cjs` extending `plugin:@typescript-eslint/recommended`, `plugin:react-hooks/recommended`, and `plugin:react/recommended`; set `react/react-in-jsx-scope: off` (React 17+ automatic JSX transform)
4. Verify `vite.config.ts` references the `@vitejs/plugin-react` plugin; add `build.rollupOptions` to ensure chunk hashing is enabled (default in Vite production build)
5. Insert IBM Plex Sans `<link rel="preconnect">` and `<link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600&display=swap" rel="stylesheet">` tags in `frontend/index.html` `<head>`
6. Add `font-family: 'IBM Plex Sans', sans-serif;` to `body` selector in `frontend/src/index.css`
7. Replace boilerplate `App.tsx` with a minimal shell component that returns a `<div id="app-root">` placeholder; confirm `npm run build` produces zero errors and `dist/index.html` exists with hashed asset filenames

---

## Current Project State
```
frontend/
├── index.html                    (CREATE)
├── package.json                  (CREATE)
├── tsconfig.json                 (CREATE)
├── tsconfig.node.json            (CREATE)
├── vite.config.ts                (CREATE)
├── .eslintrc.cjs                 (CREATE)
└── src/
    ├── main.tsx                  (CREATE)
    ├── App.tsx                   (CREATE)
    └── index.css                 (CREATE)
```

---

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | frontend/index.html | SPA HTML entry with IBM Plex Sans Google Fonts link tags in `<head>` |
| CREATE | frontend/package.json | React 18, TypeScript, Vite, ESLint dependencies and npm scripts |
| CREATE | frontend/vite.config.ts | Vite config with `@vitejs/plugin-react` plugin; production hashing enabled by default |
| CREATE | frontend/tsconfig.json | TypeScript strict-mode compiler config targeting ES2022 modules |
| CREATE | frontend/.eslintrc.cjs | ESLint rules extending `@typescript-eslint/recommended` and `react-hooks/recommended` |
| CREATE | frontend/src/main.tsx | `ReactDOM.createRoot` render entry mounting `<App />` into `#root` |
| CREATE | frontend/src/App.tsx | Minimal root shell component — placeholder `<div>` only (routing added in task_002) |
| CREATE | frontend/src/index.css | Global styles with `body { font-family: 'IBM Plex Sans', sans-serif; }` |

---

## External References
- https://vitejs.dev/guide/ (Vite 5 project guide)
- https://react.dev/learn/typescript (React 18 TypeScript setup)
- https://typescript-eslint.io/getting-started/ (typescript-eslint v6 setup)
- https://fonts.google.com/specimen/IBM+Plex+Sans (IBM Plex Sans Google Fonts embed instructions)

---

## Build Commands
- Refer to applicable technology stack build commands: [.propel/build/](.propel/build/)

---

## Implementation Validation Strategy
- [ ] `cd frontend && npm run build` exits with code 0; `dist/index.html` exists and `dist/assets/` contains at least one file with a hash in its name (AC-001)
- [ ] `cd frontend && npm run lint` exits with code 0 — zero ESLint errors (AC-001)

---

## Implementation Checklist
- [ ] `frontend/` Vite React TypeScript project initialised; all boilerplate demo files (counter, `App.css`, logo SVG) removed (AC-001)
- [ ] `tsconfig.json` sets `"strict": true`, `"noUnusedLocals": true`, `"noUnusedParameters": true`; TypeScript version ≥ 5.x installed in `devDependencies` (AC-001)
- [ ] `.eslintrc.cjs` extends `plugin:@typescript-eslint/recommended` and `plugin:react-hooks/recommended`; `npm run lint` passes with zero errors on the initial scaffold (AC-001)
- [ ] `package.json` `"scripts"` contains `"build": "tsc && vite build"` so TypeScript compilation errors block the build (AC-001)
- [ ] `frontend/index.html` `<head>` contains `<link rel="preconnect" href="https://fonts.googleapis.com">` and the IBM Plex Sans stylesheet link (AC-005)
- [ ] `frontend/src/index.css` `body` selector sets `font-family: 'IBM Plex Sans', sans-serif;` and the file is imported in `main.tsx` (AC-005)
- [ ] `npm run build` produces `dist/index.html` and at least one hashed JS asset in `dist/assets/` — verified by listing `dist/` after build (AC-001)
