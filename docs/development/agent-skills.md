# Agent skills for Tome Raider

Checked 2026-11-01. The repository is already configured for the Matt Pocock engineering workflow through root `AGENTS.md`, `docs/agents/`, the glossary, ADRs, and GitHub Issues. The skills themselves are available through the plugin in the current conversation; project-local copies are not required for that setup.

## Recommended local Codex setup

Matt's current installation guide recommends choosing one installation method per agent: the plugin, or editable copied skill folders. For a local Codex installation, its documented plugin commands are:

```sh
codex plugin marketplace add mattpocock/skills
codex plugin add mattpocock-skills@mattpocock
```

The plugin is the recommended default for this project. It avoids committing a second copy of the general workflows; repository-specific rules remain in `AGENTS.md` and its linked documents. These commands have not been executed against the owner's local computer by this session.

## Optional repository-local copies

Use repository-local skills when intentionally keeping editable, version-controlled workflows with the code. Codex discovers repository skills at `.agents/skills/<skill-name>/SKILL.md`, including the supporting files belonging to each skill. Root `AGENTS.md` continues to carry project instructions.

Choose the copied-folder approach instead of a duplicate installation of the same skills for that agent. Keep the upstream source/version and required supporting files when adopting copies. The current change documents the choice; it does not install or modify personal or repository-local skill packages.

The existing repository setup does not need to be repeated merely to install the plugin on another machine. Keep the agreed tracker, `CONTEXT.md` glossary, ADR layout, and one-question pacing conventions when applying newer upstream defaults.

Sources:

- [Matt Pocock skills: installation methods and per-repository setup](https://github.com/mattpocock/skills#installation-30-second-setup)
- [Official Codex skill discovery and plugin guidance](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills)
