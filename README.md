# Power Position Service

A .NET Worker Service that runs as a Windows Service to produce daily power-position CSV extracts from a PowerDay API.

## Overview

This service is designed for **power-trading systems** where:

- A **power day** starts at 23:00 London local time
- Period 1 corresponds to 23:00, Period 24 to 22:00
- The service produces a snapshot CSV for each power day

## Scheduling

### Daily Run Time

The service runs **once per day** at a configured London local time (default: **23:05 Europe/London**).

### Power Day Calculation

On each run, the service targets the **next power day**:

```
powerDay = London calendar date + 1
```

For example, if the service runs at 23:05 on January 15th (London time), it processes power day 2024-01-16.

### Startup Behavior

On startup, the service immediately processes any pending jobs before waiting for the scheduled time. This ensures:

- Recovery from service restarts
- Catch-up after downtime
- No missed extracts

## Reliability Model

### File-Based Durable Work Queue

The service uses a filesystem-based state model to ensure **no power day extract is ever missed**:

```
<OutputDirectory>/
├── pending/     # Power days awaiting processing
│   └── YYYYMMDD.job
├── done/        # Successfully completed power days (audit trail)
│   └── YYYYMMDD.job
└── out/         # Final CSV output files
    └── PowerPosition_YYYYMMDD.csv
```

### Processing Algorithm

1. **Calculate next power day** using Europe/London time zone
2. **Create pending job** if not already done:
   - If `done/YYYYMMDD.job` doesn't exist, ensure `pending/YYYYMMDD.job` exists
3. **Process all pending jobs** (oldest → newest):
   - If output CSV exists → move job to `done/`, continue
   - Otherwise:
     - Call `PowerService.GetTrades(powerDay)`
     - Aggregate volumes across all trades (24 periods)
     - Write CSV atomically (temp file → rename)
     - Move job from `pending/` to `done/`
   - On failure → leave job in `pending/`, stop processing

### Replay Mechanism

If the API is unavailable:

1. The job remains in `pending/`
2. On next scheduled run (or service restart), all pending jobs are retried
3. Jobs are processed in chronological order to maintain consistency

## CSV Output Format

**Filename**: `PowerPosition_YYYYMMDD.csv` (using power day date)

**Format**:

```csv
Local Time,Volume
23:00,1234.56
00:00,2345.67
01:00,3456.78
...
22:00,4567.89
```

- Header row: `Local Time,Volume`
- 24 data rows, ordered by local time starting at 23:00
- Times in 24-hour format
- Volumes rounded to 2 decimal places

## Period-to-Time Mapping

| Period | Local Time |
|--------|------------|
| 1      | 23:00      |
| 2      | 00:00      |
| 3      | 01:00      |
| ...    | ...        |
| 24     | 22:00      |

## Configuration

Edit `appsettings.json`:

```json
{
  "PowerPositionService": {
    "DailyRunTime": "23:05",
    "TimeZone": "Europe/London",
    "PowerDayApiUrl": "http://example",
    "OutputDirectory": ".",
    "EnableFileLog": true,
    "LogDirectory": "."
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `DailyRunTime` | `23:05` | Time to run daily (HH:mm, 24-hour) |
| `TimeZone` | `Europe/London` | IANA time zone for scheduling |
| `PowerDayApiUrl` | `http://example` | Base URL for PowerDay API |
| `OutputDirectory` | `.` | Root for pending/done/out directories |
| `EnableFileLog` | `true` | Write logs to file (in addition to console) |
| `LogDirectory` | `.` | Directory for log files |

### Mock Mode

If `PowerDayApiUrl` is `http://example` or contains `mock`, the service uses a mock implementation that generates sample data. Configure a real API URL for production.

## Concurrency

- **Single instance** enforced via OS-level named mutex (`Global\PowerPositionService_SingleInstance`)
- Job processing is **sequential** (oldest pending first)
- **Atomic writes** prevent consumers from seeing partial CSV files

## Building

```bash
dotnet build
```

## Running as Console Application

```bash
dotnet run
```

## Installing as Windows Service

```bash
# Publish
dotnet publish -c Release -o ./publish

# Install service (run as Administrator)
sc create "PowerPositionService" binPath="C:\path\to\publish\PowerPositionService.exe"

# Start service
sc start PowerPositionService

# Stop service
sc stop PowerPositionService

# Uninstall
sc delete PowerPositionService
```

## Logs

When `EnableFileLog=true`, logs are written to:

- Console (stdout/stderr)
- Rolling log files: `<LogDirectory>/PowerPositionService-YYYYMMDD.log`

Log files are retained for 30 days by default.

## API Contract

The service expects an API implementing:

```csharp
// GET /trades?powerDay=YYYY-MM-DD
// Returns: List<Trade>

public class Trade
{
    public string TradeId { get; set; }
    public DateTime PowerDay { get; set; }
    public double[] Periods { get; set; } // 24 values
}
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Worker                               │
│  (BackgroundService with mutex + scheduler loop)            │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│                     JobProcessor                            │
│  (Orchestrates: power day calc → pending check → process)   │
└────┬────────────────┬─────────────────┬─────────────────────┘
     │                │                 │
     ▼                ▼                 ▼
┌─────────┐    ┌─────────────┐    ┌───────────┐
│Scheduler│    │JobQueueSvc  │    │IPowerSvc  │
│(timing) │    │(pending/    │    │(API call) │
│         │    │ done/out)   │    │           │
└─────────┘    └─────────────┘    └───────────┘
                                       │
                                       ▼
                              ┌──────────────────┐
                              │PositionAggregator│
                              │(sum 24 periods)  │
                              └────────┬─────────┘
                                       │
                                       ▼
                              ┌─────────────────┐
                              │   CsvWriter     │
                              │(atomic writes)  │
                              └─────────────────┘
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| API unavailable | Job stays in `pending/`, retry on next run |
| Partial API response | Processing fails, retry later |
| Service crash | Mutex abandoned, next start recovers |
| Duplicate instance | Second instance exits immediately |
| Output already exists | Job marked done, skip processing |

## Docker

## Build

```bash
docker build -t power-position-service .
```

## Run

Basic run:

```bash
docker run power-position-service:latest
```

Run in background:

```bash
docker run -d --name power-position-service power-position-service:latest
```

## Environment Variable Overrides

Environment variables override `appsettings.json` values. Use double underscore `__` to represent nested config sections.

**Override API URL:**

```bash
docker run -e PowerPositionService__PowerDayApiUrl=http://api.example.com:8080 power-position-service:latest
```

**Change scheduled run time:**

```bash
docker run -e PowerPositionService__DailyRunTime=06:30 power-position-service:latest
```

**Change timezone:**

```bash
docker run -e PowerPositionService__TimeZone=America/New_York power-position-service:latest
```

**Disable file logging:**

```bash
docker run -e PowerPositionService__EnableFileLog=false power-position-service:latest
```

**Multiple overrides:**

```bash
docker run \
  -e PowerPositionService__PowerDayApiUrl=http://api.example.com:8080 \
  -e PowerPositionService__DailyRunTime=06:30 \
  -e PowerPositionService__TimeZone=America/New_York \
  -e PowerPositionService__EnableFileLog=true \
  power-position-service:latest
```

## Persistent Storage

Mount volumes to persist state across container restarts:

```bash
docker run -d \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/logs:/app/logs \
  power-position-service:latest
```

This persists:

- `/app/data/pending/` - pending jobs
- `/app/data/done/` - completed jobs
- `/app/data/out/` - CSV output files
- `/app/logs/` - log files

## Production Example

Full production setup with all options:

```bash
docker run -d \
  --name power-position-service \
  --restart unless-stopped \
  -e PowerPositionService__PowerDayApiUrl=http://powerday-api:8080 \
  -e PowerPositionService__DailyRunTime=23:05 \
  -e PowerPositionService__TimeZone=Europe/London \
  -e PowerPositionService__EnableFileLog=true \
  -v /opt/power-position/data:/app/data \
  -v /opt/power-position/logs:/app/logs \
  power-position-service:latest
```

## View Logs

```bash
# Follow logs
docker logs -f power-position-service

# Last 100 lines
docker logs --tail 100 power-position-service
```

## Check Output

```bash
# List generated CSVs
docker exec power-position-service ls -la /app/data/out/

# View a CSV
docker exec power-position-service cat /app/data/out/PowerPosition_20251217.csv

# Check pending jobs
docker exec power-position-service ls -la /app/data/pending/

# Check completed jobs
docker exec power-position-service ls -la /app/data/done/
```
