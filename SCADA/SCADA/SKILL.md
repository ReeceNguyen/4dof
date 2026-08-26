---
name: plan-first
description: Use ONLY when the user explicitly asks to plan first, e.g. types "lên kế hoạch", "lập plan", "viết plan trước", "làm plan mode", "plan this", "make a plan first", or "/plan". Do NOT trigger automatically on regular implement/build/fix/refactor requests — only when the user's own words explicitly ask for a plan.
---

## Objective
Force a plan-then-execute workflow. Never write or edit code before the plan is reviewed and approved.

## Steps
1. Read relevant files first to understand existing patterns.
2. Write an Implementation Plan artifact containing:
   - Goal & scope
   - Files to create/modify, with reasoning
   - Ordered implementation steps
   - Risks / trade-offs
   - Test/verification approach
3. Stop. Do not write any code yet.
4. Ask the user to review and reply "Approved" or give feedback.
5. If feedback is given, revise the plan and go back to step 4.
6. Only after explicit approval, proceed to implement following the plan exactly.

## Rules of engagement
- Never skip the plan step, even for requests that "seem simple," unless the user explicitly says "skip plan" or "fast mode."
- If the user interrupts with new instructions mid-plan, update the plan before continuing.