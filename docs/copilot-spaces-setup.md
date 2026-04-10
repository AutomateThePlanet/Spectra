---
title: Copilot Spaces Setup
parent: Deployment
nav_order: 3
---

# Copilot Spaces Integration

How to use GitHub Copilot Spaces for inline documentation lookup during test execution.

Related: [Execution Agent](execution-agent/overview.md) | [Configuration](configuration.md)

---

## What Are Copilot Spaces?

[Copilot Spaces](https://github.com/copilot/spaces) are curated collections of documentation, code, and other resources that give GitHub Copilot additional context. When configured with SPECTRA, the execution agent can look up product documentation mid-execution to help testers understand test steps, expected behavior, and domain terminology.

## Why Use Spaces During Execution?

During manual test execution, testers often need clarification:

- "What does this step mean?"
- "How do I navigate to this screen?"
- "What should I see after step 5?"

Without Spaces, the tester must leave the execution flow to search documentation. With Spaces, the execution agent answers these questions inline using your product docs as the source of truth.

## Setup

### 1. Create a Copilot Space

1. Go to [github.com/copilot/spaces](https://github.com/copilot/spaces)
2. Click **New Space**
3. Give it a descriptive name (e.g., `product-docs`, `myapp-documentation`)
4. Add your product documentation as sources:
   - Repository files (Markdown docs, wikis)
   - Uploaded documents (PDFs, design specs)
   - URLs (hosted documentation sites)
5. Save the Space

### 2. Share with Your Team

For team-wide access, create the Space under your GitHub organization:

1. Select your organization as the owner when creating the Space
2. Set visibility to **Organization** so all members can access it
3. Team members using the SPECTRA execution agent will automatically have access

### 3. Configure in spectra.config.json

Add the Space reference to your project configuration:

```json
{
  "execution": {
    "copilot_space": "product-docs",
    "copilot_space_owner": "your-org"
  }
}
```

| Property | Description |
|----------|-------------|
| `copilot_space` | Name of the Copilot Space to use for documentation lookup |
| `copilot_space_owner` | GitHub user or organization that owns the Space |

Both fields are optional. When not configured, the agent uses `list_copilot_spaces` to discover available Spaces.

### 4. Install the GitHub MCP Server in VS Code

The execution agent needs access to the GitHub MCP tools (`get_copilot_space`, `list_copilot_spaces`). If you're using VS Code with Copilot Chat:

1. Ensure the [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot) extension is installed
2. The GitHub MCP server tools are available automatically through Copilot Chat
3. SPECTRA's agent prompt already declares the required tools in its frontmatter

For other MCP clients, ensure the GitHub MCP server is configured and the `get_copilot_space` and `list_copilot_spaces` tools are available.

## How It Works

During test execution, when the tester asks a clarification question:

1. The agent checks `execution.copilot_space` in spectra.config.json
2. If configured, it calls `get_copilot_space` with the Space name to retrieve relevant documentation
3. If not configured, it calls `list_copilot_spaces` to find available Spaces
4. The agent cross-references the test case's `source_refs` to find the most relevant documentation
5. A concise answer is provided inline — the tester stays in the execution flow

## Example

```
Agent: ## TC-205: Apply discount code at checkout
       ### Steps
       1. Add items totaling $100+ to cart
       2. Navigate to checkout
       3. Enter discount code "SAVE20" in the promo field
       4. Verify the discount is applied to the subtotal

       What is the result? (pass/fail/blocked/skip)

Tester: What should the discount look like? Is it 20% or $20?

Agent: [looks up product docs via Copilot Space]
       According to the product documentation, "SAVE20" is a percentage
       discount — it applies 20% off the subtotal. For a $100 cart,
       you should see a $20.00 discount line item and a $80.00 new subtotal.

Tester: p
```

## Tips

- **Keep Spaces focused** — A Space with your product documentation works better than one with your entire repository
- **Update regularly** — When product behavior changes, update the Space sources so the agent gives accurate answers
- **Use source_refs** — Test cases with `source_refs` in their frontmatter help the agent find the right documentation faster
