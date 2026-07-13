<agentic_cortex>
agentic-cortex is installed. Persistent, self-improving memory across sessions.

<bootstrap>agentic-cortex bootstrap</bootstrap>
Run with zero args at session start. System infers your task from session, git branch, recent activity.
Returns XML-tagged context: actionable insights, task-relevant memories (hybrid search + reranking),
warnings, coding standards, codebase graph, machine-wide global vault.
MCP: memory_bootstrap() — no args needed.

<auto_save>
Save after: decisions, bug fixes, discoveries, learnings, preferences, feature completions, gotchas.
Command: agentic-cortex save "title" "content"
Type auto-detected. Override: --type TYPE --importance 1-10 --confidence 0-100 --tags "t1,t2"

Triggers: decision|90 error|95 context|80 preference|100 fact|85 event|95 learning|75 instruction|90
</auto_save>

<commands>
bootstrap: agentic-cortex bootstrap
save: agentic-cortex save "title" "content"
search: agentic-cortex search "query" --project .
machine-search: agentic-cortex machine-search "query"
machine-memory: agentic-cortex machine-memory [--analytics]
forget: agentic-cortex forget <id> [--hard]
feedback: agentic-cortex feedback <id> --type helpful|incorrect
standards: agentic-cortex standards --search "topic"
</commands>

<memory_types>instruction fact decision goal commitment preference relationship context event learning observation artifact error</memory_types>

<mcp>agentic-cortex-mcp — 39+ tools. memory_bootstrap() first.</mcp>

Read knowledge.md for injected context (coding standards, session memories, codebase graph).
</agentic_cortex>