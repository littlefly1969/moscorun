import js from "@eslint/js";
import globals from "globals";

export default [
  { ignores: ["node_modules", "coverage"] },
  js.configs.recommended,
  {
    files: ["**/*.{js,mjs}"],
    languageOptions: { ecmaVersion: 2024, sourceType: "module", globals: globals.node },
    rules: {
      eqeqeq: ["error", "always"],
      "no-implicit-coercion": "error",
      "no-param-reassign": "error",
      "no-var": "error",
      "prefer-const": "error",
      "no-unused-vars": ["error", { argsIgnorePattern: "^_" }],
      "no-console": ["error", { allow: ["error"] }],
    },
  },
];
