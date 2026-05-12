# Product Promotion Workflow

This folder separates experimental development from the consumer-facing product
package.

## Rule

Development features do not ship just because they exist in `src/`.

To promote a feature, add it to `product_manifest.json` and provide its product
guide/config/tool fragments under `fragments/`.

## Promotion Unit

Each promoted feature should declare:

- feature id
- product-facing name
- guide fragment, if any
- config templates, if any
- product tools, if any

The assembly script then creates the product package from the manifest.

## Dev-Only Features

Dev-only features should remain absent from `product_manifest.json`.

The assembly script also scans for known excluded product terms after assembly.

## Current First Pass

This first pass makes the current product package reproducible from:

- `product_manifest.json`
- `fragments/`
- current `PRODUCT_BUILD` plugin DLL
- product sidecar source/build output

It does not yet split C# source by feature. Runtime feature gating remains
controlled by product compile symbols and product-only source cleanup.

## Generated Package

`package/` is generated output. The assembler clears and rebuilds it each run
so removed features do not leave stale files behind.
