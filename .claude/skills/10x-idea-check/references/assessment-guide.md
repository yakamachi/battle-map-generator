# Assessment guide (draft v1)

## Source authority and known conflicts

The bundled certification and dates documents are verbatim snapshots of the
repository sources. This guide translates them into early idea assessment; it
does not amend certification policy. Cite the source filename when discussing
a disputed requirement.

- Dates: use `10xdevs-4-dates.md`, including its 23:59 cutoffs and two-week review
  window. Ignore the old July date and July/August/September example in certification
  prose. The dates source does not specify a timezone. Europe/Warsaw is a planning
  assumption, not a documented rule; near a cutoff, advise confirming the timezone.
- Deployment: the Builder introduction says cloud deployment, but the explicit
  optional list and non-web exceptions allow other delivery forms. Do not impose
  a public URL on every idea. If a case hinges on conflicting wording, name the
  conflict and formulate a question for mentors rather than guaranteeing acceptance.
- Documents: the source says contextual documents, with filenames introduced by
  "np.". Treat PRD/infrastructure/roadmap as examples, not an invented exact-file gate.
- Timeline: certification's one-week first-flow advice and shape's three-week
  cost warning are planning heuristics, not eligibility criteria. Neither is a
  universal cap. Calibrate to available time, progress, skill, and agent workflow.
- CI/CD belongs explicitly to Champion, though general prose mentions automation.
  Do not convert it into an extra Builder requirement during idea screening.
- Certification closes with "Nie weryfikujemy stopnia wykorzystania AI", while
  also emphasizing AI in the development process. Do not invent token quotas,
  agent counts, or required AI product features.
- The certification source refers to a project comparison table that is absent
  from the Markdown. Do not invent its rows or infer niche-stack disqualification.

## What counts as worth shaping

There is a plausible useful outcome, a reason the participant cares, and a path
to exploring a deliverable version. Learning, helping one person, or improving
one's own workflow are sufficient motivations. Do not require startup validation.

Shaping should resolve exact user flows, domain rules, role boundaries, detailed
scope, non-goals, and success criteria. Do not demand those as entry conditions.
Ask about a vague benefit only when there is not yet enough to identify any
useful direction. Lack of detail is different from lack of access or feasibility.

An essential inaccessible API, unavailable hardware, or missing permission can
make discovery premature. Propose a focused check or a fallback that retains value.
Do not require access to real customers or commercial traction for a course project.

## Calibrate ambition

Use demonstrated development and agent-delivery experience as separate signals:

| Context | Guidance |
| --- | --- |
| New to development and agents | Prefer visible progress, few dependencies, and outcomes they can verify. |
| Experienced developer, new to agents | Encourage a meaningful problem in a familiar domain, with room to learn supervision and recovery. |
| Confident prompting, limited engineering experience | Check how they recognize correctness; do not infer delivery ability from prompting fluency. |
| Has shipped substantial agent-assisted work | Support ambitious MVPs when integration, verification, and time constraints are credible. Suggest one valuable stretch if they want a challenge. |

Use these examples to guide the conversation. A small project can suit an expert. A beginner can explore an ambitious direction through a
manageable first milestone. For existing projects, assess what changes, what already works, and what must
remain intact.

Account for work the participant can complete faster with agents. Ask about the actual loop:
what the participant delegates, how they test, and how they recover from failures.
Do not assume all coding is manual or that any named model removes external
dependencies, integration work, or the need for judgment.

## Time and submission strategy

Use the intended deadline, current date, weekly availability, and current progress.
Calendar time alone is insufficient. If useful, calculate a rough capacity range
from weeks remaining × available hours, clearly labeled as capacity, not an effort
estimate. Leave room for testing, documentation, integration, and submission.
Do not assign made-up feature estimates or mandatory percentage buffers.

Separate a first working milestone that exposes the biggest uncertainty from
the intended MVP. An ambitious MVP can have a very small first milestone.

Offer concrete choices when time is tight: reduce the submission scope, remove
one dependency, or consider a later listed deadline. Explain that distinction is
limited to the first deadline without implying ambition earns it automatically.

The certification source says the first submission determines the assessed scope.
Do not recommend submitting Builder now and adding Architect/Champion at a later
deadline as a certification strategy. Later development can be personal continuation;
choosing a later initial submission is a different option. The two-week review
window is not an automatic extension or guaranteed resubmission opportunity.

## Early course-fit check

For Builder, look for a plausible future path to:

- Access control suitable for the application type.
- Creating, reading, updating, and deleting data sensibly for the domain.
- A domain rule or business logic, with or without AI in the product.
- Contextual documentation.
- At least one test verifying behavior from the user's perspective.

Check whether the idea can eventually meet these requirements. Avoid
full pass/fail checklists on an unbuilt idea. If a CRUD list has no apparent domain
rule, say what Builder expects and let shaping explore a useful rule. Do not call
the idea worthless or silently add an invented feature to make it qualify.

Do not waive an operation or access requirement on your own. Where sensible domain
adaptation is uncertain, state the exact question for mentors. Non-web applications,
private/company projects, and work on existing systems are explicitly supported;
access and acceptable evidence may require clarification. Do not request company
secrets or uploads of private code to perform this conversational assessment.

Architect and Champion may be learning goals, but detailed evidence requirements
are deferred to their lessons. Explain what the source says about those paths. Do not invent artifact
requirements or predict which badges the participant will receive.
