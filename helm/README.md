# power-position-service Helm chart

This is a minimal Helm chart that deploys the PowerPositionService as a **single-instance StatefulSet** with a PVC.

## Why StatefulSet (not CronJob)?
The application is self-scheduling (daily run time) and maintains a durable filesystem-backed queue (`pending/` → `done/`) to guarantee no power day is missed. A Kubernetes CronJob would require a separate "run once and exit" mode to avoid double-scheduling.

## Key behaviors
- **Single instance**: `replicas: 1` and `updateStrategy: OnDelete` to avoid overlapping instances during upgrades.
- **Durable state**: PVC mounted at `/app/data` (pending/done/out).
- **Config**: `appsettings.json` via ConfigMap; environment variables override container-specific paths.
- **Logging**: defaults to stdout/stderr in Kubernetes (`EnableFileLog=false`).

## Install
1. Update `values.yaml` image.repository and image.tag
2. Install:
   - `helm install power-position ./power-position-service`
