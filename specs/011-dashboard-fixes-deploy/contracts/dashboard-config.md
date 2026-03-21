# Contract: Dashboard Configuration Schema

**Date**: 2026-03-21

## spectra.config.json — dashboard section

```json
{
  "dashboard": {
    "output_dir": "./site",
    "title": null,
    "template_dir": null,
    "cloudflare_project_name": "spectra-dashboard",
    "include_coverage": true,
    "include_runs": true,
    "max_trend_points": 30
  }
}
```

### Field Specifications

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| output_dir | string | No | "./site" | Output directory for generated dashboard |
| title | string | No | null (uses repo name) | Dashboard page title |
| template_dir | string | No | null (uses built-in) | Path to custom template directory |
| cloudflare_project_name | string | No | "spectra-dashboard" | Cloudflare Pages project name for deployment |
| include_coverage | boolean | No | true | Include coverage visualization in dashboard |
| include_runs | boolean | No | true | Include run history in dashboard |
| max_trend_points | integer | No | 30 | Maximum number of trend data points to display |

### Consumers

- **CLI**: `spectra dashboard` reads `output_dir`, `title`, `template_dir`, `include_coverage`, `include_runs`, `max_trend_points`
- **GitHub Action**: Reads `cloudflare_project_name` to determine deployment target
- **DataCollector**: Reads `max_trend_points` to limit trend data
