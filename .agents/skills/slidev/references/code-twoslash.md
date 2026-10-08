---
name: twoslash
description: Show TypeScript type information inline or on hover in code blocks
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# TwoSlash Integration

Show TypeScript type information inline or on hover.

## Usage

````md
```ts twoslash
import { ref } from 'vue'

const count = ref(0)
//            ^?
```
````

## Features

- Type information on hover
- Inline type annotations with `^?`
- Errors and warnings display
- Full TypeScript compiler integration

## Annotations

```ts twoslash
const count = ref(0)
//            ^?
// Shows: const count: Ref<number>
```

## Use Case

Perfect for TypeScript/JavaScript teaching materials where showing types helps understanding.

## Resources

- TwoSlash docs: https://twoslash.netlify.app/
