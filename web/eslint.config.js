// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const boundaries = require('eslint-plugin-boundaries');

/**
 * Architecture boundaries (see docs/ARCHITECTURE.md, "PWA (Angular) — feature-first").
 *
 * - `core/protocol` is generated from the agent and depends on nothing.
 * - `core/<module>/{domain,application,infrastructure}` hold the shared ports, facades and adapters.
 * - `features/<feature>/{domain,application,infrastructure,ui/pages,ui/components}` hold one feature each.
 *   Features never import each other; presentational components never reach a facade, a port or an adapter.
 * - The root files (`main.ts`, `app.ts`, `app.config.ts`, `app.routes.ts`) are the composition root and may import
 *   anything. Any other file outside these folders is an unknown file and fails the lint.
 */
const elements = [
  { type: 'protocol', pattern: 'src/app/core/protocol' },
  { type: 'core-domain', pattern: 'src/app/core/*/domain', capture: ['module'] },
  { type: 'core-application', pattern: 'src/app/core/*/application', capture: ['module'] },
  { type: 'core-infrastructure', pattern: 'src/app/core/*/infrastructure', capture: ['module'] },
  { type: 'feature-domain', pattern: 'src/app/features/*/domain', capture: ['feature'] },
  { type: 'feature-application', pattern: 'src/app/features/*/application', capture: ['feature'] },
  { type: 'feature-infrastructure', pattern: 'src/app/features/*/infrastructure', capture: ['feature'] },
  { type: 'feature-page', pattern: 'src/app/features/*/ui/pages', capture: ['feature'] },
  { type: 'feature-component', pattern: 'src/app/features/*/ui/components', capture: ['feature'] },
];

/** The composition root: bootstrap and root wiring files only. A new top-level file is an unknown file. */
const files = [
  {
    category: 'composition-root',
    pattern: ['src/main.ts', 'src/app/app.ts', 'src/app/app.config.ts', 'src/app/app.routes.ts'],
  },
];

/** Builds an element selector restricted to the importing file's own feature. */
const sameFeature = (type) => ({
  element: { type, captured: { feature: '{{from.element.captured.feature}}' } },
});

const anyOf = (...types) => types.map((type) => ({ element: { type } }));

const policies = [
  // Third-party packages (Angular, RxJS, ...) are allowed everywhere.
  { allow: { to: { module: { origin: 'external' } } } },
  { allow: { to: { module: { origin: 'core' } } } },

  {
    from: { file: { categories: 'composition-root' } },
    allow: { to: [{ element: { type: '*' } }, { file: { categories: 'composition-root' } }] },
  },
  { from: { element: { type: 'protocol' } }, allow: { to: anyOf('protocol') } },

  { from: { element: { type: 'core-domain' } }, allow: { to: anyOf('protocol', 'core-domain') } },
  {
    from: { element: { type: 'core-application' } },
    allow: { to: anyOf('protocol', 'core-domain', 'core-application') },
  },
  {
    from: { element: { type: 'core-infrastructure' } },
    allow: { to: anyOf('protocol', 'core-domain', 'core-application', 'core-infrastructure') },
  },

  {
    from: { element: { type: 'feature-domain' } },
    allow: { to: [...anyOf('protocol', 'core-domain'), sameFeature('feature-domain')] },
  },
  {
    from: { element: { type: 'feature-application' } },
    allow: {
      to: [
        ...anyOf('protocol', 'core-domain', 'core-application'),
        sameFeature('feature-domain'),
        sameFeature('feature-application'),
      ],
    },
  },
  {
    from: { element: { type: 'feature-infrastructure' } },
    allow: {
      to: [
        ...anyOf('protocol', 'core-domain', 'core-application'),
        sameFeature('feature-domain'),
        sameFeature('feature-application'),
        sameFeature('feature-infrastructure'),
      ],
    },
  },
  {
    from: { element: { type: 'feature-page' } },
    allow: {
      to: [
        ...anyOf('protocol', 'core-domain', 'core-application'),
        sameFeature('feature-domain'),
        sameFeature('feature-application'),
        sameFeature('feature-page'),
        sameFeature('feature-component'),
      ],
    },
  },
  {
    from: { element: { type: 'feature-component' } },
    allow: {
      to: [
        ...anyOf('protocol', 'core-domain'),
        sameFeature('feature-domain'),
        sameFeature('feature-component'),
      ],
    },
  },
];

module.exports = defineConfig([
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    plugins: { boundaries },
    settings: {
      'boundaries/elements': elements,
      'boundaries/files': files,
      'boundaries/legacy-templates': false,
      'import/resolver': { node: { extensions: ['.ts', '.js'] } },
    },
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'app',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'app',
          style: 'kebab-case',
        },
      ],
      '@angular-eslint/prefer-on-push-component-change-detection': 'error',
      '@angular-eslint/prefer-inject': 'error',
      'boundaries/dependencies': ['error', { default: 'disallow', policies }],
      'boundaries/no-unknown-files': 'error',
    },
  },
  {
    // Generated from agent/src/LeverDeck.Protocol; never edited by hand, so style rules do not apply.
    files: ['src/app/core/protocol/**/*.ts'],
    rules: {
      '@typescript-eslint/consistent-type-definitions': 'off',
      '@typescript-eslint/consistent-indexed-object-style': 'off',
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {},
  },
]);
