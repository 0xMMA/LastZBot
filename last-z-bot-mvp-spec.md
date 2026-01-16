# Last Z Bot MVP Specification

## Overview

Android automation bot for "Last Z: Survival Shooter" - a mobile game with daily maintenance tasks. The bot handles repetitive actions so players can focus on actual gameplay.

**Stack**: .NET Aspire, redroid (Android in Docker), ADB/scripting

## Target Actions (MVP Scope)

| Action | Complexity | Value |
|--------|------------|-------|
| Claim daily rewards | Low | Never miss login bonuses |
| Complete daily tasks | Medium | Auto-claim completed task rewards |
| Alliance help | Low | Tap all help buttons (15-30/day) |
| Science queue | Medium | Keep research running 24/7 |

## Architecture

```
┌─────────────────────────────────────────────────────┐
│ .NET Aspire Orchestration                           │
├─────────────────────────────────────────────────────┤
│ Bot Service          │ Vision Service              │
│ - Action scheduling  │ - Screenshot capture        │
│ - State machine      │ - Element detection         │
│ - Retry logic        │ - OCR (text extraction)     │
├──────────────────────┼─────────────────────────────┤
│ redroid Container    │ Data Store                  │
│ - Android emulator   │ - Action history            │
│ - ADB interface      │ - Detection patterns        │
│ - Game installed     │ - Performance metrics       │
└─────────────────────────────────────────────────────┘
```

## Detection Strategy

Three methods, selected by historical success rate:

1. **Cached Coordinates** - Fast, free, breaks on UI changes
2. **Template Matching** - Robust, needs reference images
3. **AI Vision** - Expensive fallback, handles unknowns

```
Method Selection:
score = success_rate / (avg_time_ms + cost_weight)
→ Use highest scoring method
→ Fall back to next if failure
```

Track per method: `success_count`, `failure_count`, `last_success`, `avg_time_ms`

## Action Flows

### 1. Claim Daily Rewards

```
1. Launch app / verify main screen
2. Detect reward popup OR navigate to rewards
3. Tap "Claim" button
4. Dismiss confirmation popup
5. Verify: popup gone or reward animation
```

### 2. Complete Daily Tasks

```
1. Navigate: Main → Tasks screen
2. For each task row:
   a. Read task text (OCR)
   b. If status = "Claim": tap claim button
   c. If status = "Completed" but unclaimed: tap
   d. Skip "Go" tasks (require gameplay)
3. Return to main screen
```

### 3. Alliance Help

```
1. Navigate: Main → Alliance → Help tab
2. Detect all "Help" buttons (multi-instance)
3. Tap each sequentially (top to bottom)
4. Stop when: no buttons left OR "limit reached" message
```

### 4. Science Queue

```
1. Navigate: Main → Alliance → Science
2. Check current research status:
   - If complete: tap "Claim"
   - If queue empty: select next research
3. Research selection: use configured priority list
4. Confirm selection
```

## Popup Handling

Games spam popups. Before every action:

```
1. Take screenshot
2. Check for known popup patterns (news, ads, rewards, errors)
3. If popup detected:
   - Find dismiss button (X, OK, Close)
   - Tap dismiss
   - Re-verify screen state
4. Proceed with intended action
```

## Session Management

```
States:
- LOGGED_IN: Normal operation
- LOGGED_OUT: Re-authenticate
- MAINTENANCE: Wait and retry
- CONFLICT: Human playing, bot yields

Health check before action sequence:
→ Verify logged in (check for login screen elements)
→ If logged out: execute login flow
```

## Multi-Instance Coordination

Problem: Game allows one session. Bot and human conflict.

```
Shared state (Redis/similar):
- lock_holder: "bot" | "human" | null
- lock_acquired_at: timestamp
- bot_state: "idle" | "executing" | "finishing"

Protocol:
1. Human signals "wants_control"
2. Bot completes current action (if safe)
3. Bot releases lock, enters standby
4. Bot periodically checks for lock availability
```

## Error Recovery

```
On failure:
1. Capture screenshot + state
2. Classify failure type:
   - element_not_found → try alternative method
   - timeout → retry with delay
   - unexpected_state → restart action sequence
3. Log for analysis
4. After N consecutive failures → alert human
```

## Learning Loop

```
AI Bootstrapping:
1. New action/unknown state → use AI vision
2. AI succeeds → extract pattern (template/OCR target)
3. Store pattern with coordinates
4. Next time → try cheap method first
5. Track success rates → optimize over time

Result: Cost decreases as patterns accumulate
```

## MVP Success Criteria

- [ ] All 4 actions complete without human intervention
- [ ] Success rate ≥90% after learning phase
- [ ] Cost per action decreases 50%+ after 100 executions
- [ ] Survives one app update automatically
- [ ] Runs 7 days continuously

## Configuration

```yaml
schedule:
  daily_rewards: "06:00"
  daily_tasks: "06:05"
  alliance_help: "06:15, 12:00, 18:00"  # if quota resets
  science_check: "every 4 hours"

science_priority:
  - "Speed Research"
  - "Combat Research"
  - "Resource Production"

alerts:
  consecutive_failures: 5
  channel: "telegram"  # or webhook

human_coordination:
  enabled: true
  yield_timeout_seconds: 30
```

## Data Model

```sql
-- Action execution history
CREATE TABLE action_log (
    id SERIAL PRIMARY KEY,
    action_type VARCHAR(50),
    method_used VARCHAR(30),
    success BOOLEAN,
    duration_ms INT,
    cost_usd DECIMAL(10,6),
    ui_signature VARCHAR(64),
    error_type VARCHAR(50),
    created_at TIMESTAMP
);

-- Detection patterns
CREATE TABLE patterns (
    id SERIAL PRIMARY KEY,
    action_type VARCHAR(50),
    method VARCHAR(30),
    pattern_data JSONB,  -- coords, template path, OCR text
    success_count INT DEFAULT 0,
    failure_count INT DEFAULT 0,
    last_success TIMESTAMP,
    ui_signature VARCHAR(64)
);
```

## Implementation Order

1. **Week 1**: redroid + ADB working, screenshot capture, basic tap execution
2. **Week 2**: Template matching + OCR, daily rewards action complete
3. **Week 3**: All 4 actions, method competition, metrics collection
4. **Week 4**: Error recovery, coordination protocol, 7-day test run

## Out of Scope (MVP)

- Actual combat/mission automation
- Resource optimization decisions
- Multiple account support
- UI for monitoring (CLI/logs only)
- Cross-platform (Android only)
