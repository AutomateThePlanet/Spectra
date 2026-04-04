# Research: Bundled SKILLs and Agent Prompts

## R1: SKILL File Storage Strategy

**Decision**: Store SKILL content as static string constants in C# classes
**Rationale**: Embedded resources require build-time configuration and are harder to read/maintain. String constants are simple, editable, and the files are small (< 2KB each).
**Alternatives considered**: Embedded resources (rejected — complexity), external template files (rejected — deployment overhead)

## R2: Hash Algorithm for Modification Detection

**Decision**: SHA-256 hash of file content
**Rationale**: SHA-256 is built into .NET, collision-resistant, and standard for content integrity checks. We store the expected hash alongside the file path.
**Alternatives considered**: MD5 (rejected — weaker), file timestamp comparison (rejected — unreliable across git operations)

## R3: Hash Storage Location

**Decision**: Store expected hashes in a `.spectra/skills-manifest.json` file created during init
**Rationale**: A manifest file keeps expected hashes alongside the session state dir. It's simple to read/write and doesn't pollute the SKILL files themselves.
**Alternatives considered**: Embedded in SKILL file frontmatter (rejected — modifying the file changes its hash), separate hash file per SKILL (rejected — file proliferation)
