# Repository Instructions

## PR Workflow

- Use draft-PR-first development (`pr_first: true`).
- For multi-slice work, create a topic branch and a draft umbrella PR targeting
  `main`. Each independently shippable slice uses its own worktree and branch,
  with a draft PR targeting the topic branch before implementation.
- Small, low-risk slice PRs may auto-merge into the topic branch after review
  and successful checks. Require human approval for risky changes.
- Require human approval before merging any topic branch into `main`.
- Use squash merges for slice PRs.

## Subagents

- Default implementation and review subagents to GPT-5.6 Sol, high reasoning,
  with the long-context tier.
