---
name: plantuml
description: Create UML diagrams from text descriptions
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# PlantUML Diagrams

Create UML diagrams from text descriptions.

## Basic Usage

````md
```plantuml
@startuml
Alice -> Bob : Hello!
@enduml
```
````

## Server Configuration

Default: Uses https://www.plantuml.com/plantuml

Custom server in headmatter:

```md
---
plantUmlServer: https://your-server.com/plantuml
---
```

## Diagram Types

- Sequence diagrams
- Class diagrams
- Activity diagrams
- Component diagrams
- State diagrams
- Object diagrams
- Use case diagrams

## Resources

- PlantUML docs: https://plantuml.com/
- Live editor: https://plantuml.com/plantuml
