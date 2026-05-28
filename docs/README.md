# Generator repository — developer docs

Internal documentation for **MvvmAIO.R3.SourceGenerators** (not published to the VitePress consumer site).

| Document | Audience | Contents |
|----------|----------|----------|
| [AGENTS.md](../AGENTS.md) | Humans and AI agents | Conventions, build/CI, diagnostics, **ObservableEventsGenerator** file map and pipeline |
| [design-interface-based-event-generation.md](design-interface-based-event-generation.md) | Contributors (中文) | Interface-based event codegen design, examples, **§11** source layout |
| [CHANGELOG.md](../CHANGELOG.md) | Everyone | Released and unreleased product changes |

**User-facing** API and **R3SG** reference: [R3.SourceGenerators.Docs](https://mvvmaio.github.io/R3.SourceGenerators.Docs/).

When you change generator file layout or public behavior, update **AGENTS.md** and the design doc §11 together; update the docs site only when consumers are affected (see AGENTS.md release checklist).
