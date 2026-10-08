---
name: monaco
description: Turn code blocks into fully-featured editors
---

> Configurator authority: follow AGENTS.md and PROJECTS.md. Source-system backend examples are reference-only; absent Event layers and deployment policies do not apply.


# Monaco Editor

Turn code blocks into fully-featured editors.

## Basic Usage

````md
```ts {monaco}
console.log('HelloWorld')
```
````

## Diff Editor

Compare two code versions:

````md
```ts {monaco-diff}
console.log('Original text')
~~~
console.log('Modified text')
```
````

## Editor Height

Auto-grow as you type:

````md
```ts {monaco} {height:'auto'}
console.log('Hello, World!')
```
````

Fixed height:

````md
```ts {monaco} {height:'300px'}
// code here
```
````

## Configuration

See `/custom/config-monaco` for Monaco editor customization options.
