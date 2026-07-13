<agentic_cortex>
Persistent, self-improving agent memory. Installed globally.

<bootstrap>agentic-cortex bootstrap</bootstrap>
Zero args. Infers task. Returns XML context with memories, insights, warnings, global vault, graph.

<auto_save>
Save automatically. Never wait. Command: agentic-cortex save "title" "content"
Type auto-detected. Triggers (type|confidence): decision|90 error|95 context|80 preference|100 fact|85 event|95 learning|75 instruction|90
</auto_save>

<commands>
bootstrap: agentic-cortex bootstrap
save: agentic-cortex save "title" "content"
search: agentic-cortex search "query" --project .
machine-search: agentic-cortex machine-search "query"
machine-memory: agentic-cortex machine-memory [--analytics]
forget: agentic-cortex forget <id> [--hard]
feedback: agentic-cortex feedback <id> --type helpful|incorrect
answer: agentic-cortex answer "question" --project .
standards: agentic-cortex standards --search "topic"
daily-summary: agentic-cortex daily-summary --project .
</commands>

<memory_types>instruction fact decision goal commitment preference relationship context event learning observation artifact error</memory_types>

<mcp>agentic-cortex-mcp — 39+ tools. memory_bootstrap() with no args first.</mcp>
</agentic_cortex>