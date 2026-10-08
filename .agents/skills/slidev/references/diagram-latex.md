---
name: latex
description: Render mathematical equations using KaTeX
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# LaTeX

Render mathematical equations. Powered by KaTeX.

## Inline Math

```md
$\sqrt{3x-1}+(1+x)^2$
```

## Block Math

```md
$$
\begin{aligned}
\nabla \cdot \vec{E} &= \frac{\rho}{\varepsilon_0} \\
\nabla \cdot \vec{B} &= 0
\end{aligned}
$$
```

## Line Highlighting

```md
$$ {1|3|all}
\begin{aligned}
\nabla \cdot \vec{E} &= \frac{\rho}{\varepsilon_0} \\
\nabla \cdot \vec{B} &= 0 \\
\nabla \times \vec{E} &= -\frac{\partial\vec{B}}{\partial t}
\end{aligned}
$$
```

## Chemical Equations

Enable mhchem extension in `vite.config.ts`:

```ts
import 'katex/contrib/mhchem'

export default {}
```

Then use:

```md
$$
\ce{B(OH)3 + H2O <--> B(OH)4^- + H+}
$$
```
