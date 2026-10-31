# Planning working agreement

## Purpose

Keep the evolution of book-manager understandable through small commits and concise explanations of the decisions behind each change.

## Agreed working practices

- Work on `planning/project-foundation` for project discovery, specifications, and architecture notes.
- Use Matt Pocock Skills to guide this work.
- Commit regularly as we settle a coherent part of the plan, so the history shows how the project evolved.
- Track features, implementation tasks, bugs, and acceptance criteria in GitHub Issues for `SimonGelbart/book-manager`.
- Keep requirements, the glossary, architecture notes, and ADRs in the repository so their evolution remains visible in commits. Link issues to the relevant documents.

## Tracking decision — 2026-10-31

Use GitHub Issues for tracking work and Git for project documentation. This keeps work alongside the existing repository and lets issues connect implementation progress to documented decisions.

Local Markdown task files were considered. GitHub Issues was chosen to make work status and links to branches and pull requests visible while preserving the history of requirements and design decisions in Git.

Repository documents are the source of truth for requirements and design. Issues reference those documents and carry task-specific acceptance criteria and progress.

## Repository setup decision — 2026-10-31

Use `AGENTS.md` as the repository's agent instruction file. It points to the approved issue-tracker, triage-label, and domain-documentation conventions under `docs/agents/`.

Keep the default triage labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, and `wontfix`. This lets the Matt Pocock workflows use their standard vocabulary without a custom mapping.

The user reviewed and approved the setup before it was written. The requirements interview is the next step.

## Interview pacing decision — 2026-10-31

The user requested a sequential interview pace. The rule is recorded in `AGENTS.md` and takes precedence over the grilling skill's default question batching. This lets each answer inform the next question.

## How we will record progress

Each planning change should state what changed and why. Where relevant, include the constraints, alternatives considered, consequences, and questions still open.

Keep proposals distinguishable from agreed decisions. When new information changes a decision, explain the change in a later commit so the earlier context remains understandable.

Use one focused commit for each meaningful update, such as clarifying the first release, defining a domain term, or resolving an architectural trade-off. Commit messages should describe the change; add a body when its motivation needs explanation. Push these commits to the planning branch as we go.

## Documentation approach

Following the Matt Pocock workflow:

- Clarify requirements and design choices through `grill-with-docs`.
- Create `CONTEXT.md` when the first domain term is settled. Keep it a glossary.
- Create ADRs under `docs/adr/` when a decision is costly to reverse, needs context to understand, and reflects a real trade-off.
- Turn the agreed plan into a specification with `to-spec`.
- Break the specification into implementation tasks with `to-tickets` when appropriate.

Configure the repository's skill conventions before beginning that engineering workflow.

## Starting point — 2026-10-31

The repository began empty. A minimal README established `main`; the planning branch was then created from that baseline.

The branch, use of Matt Pocock Skills, and regular commits are agreed. Product requirements and technical choices remain open.

## Questions to resolve next

- Who will use book-manager, and what problem should it solve?
- What is the first complete, useful workflow?
- What constraints should guide its design?
