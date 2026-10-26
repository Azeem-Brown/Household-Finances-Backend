# Agent instructions

## Issue labels

The label contract for this repository is [`docs/labels.md`](./docs/labels.md). Apply it as follows:

- The planner adds the GitHub default type label (`bug`, `enhancement`, `documentation`), any matching `area: *`, and `size: *` or `priority: *` when the size and urgency are clear. It does not add workflow labels.
- The implementation agent moves the issue to `in development` when it opens the pull request and to `awaiting-review` once checks pass.
- Add `data-change` to any issue that runs a migration, reset, or destructive data operation.
- Do not invent labels. Extend `docs/labels.md` in the same pull request if a new label is needed.
- Labels are advisory. They never authorize a push, merge, or deployment.
