# BotService - Project Requirements & Architecture Outline

## Executive Summary

A SaaS platform providing automated farming services for mobile games. Users purchase credits, create "farms" (game accounts to automate), and the system executes bot runs on their behalf. The business model scales from casual users (hourly runs) to whales (24/7 dedicated automation + intelligence features).

---

## Domain Model

### Core Entities

```
USER
├── Has Credits (virtual currency)
├── Has Subscription Tier
├── Has multiple Farms
└── Has Referral relationships

FARM
├── Belongs to one User
├── Represents one game account
├── Has a Mode (Eco/Active/24/7)
├── Has encrypted game credentials
├── Is bound to one dedicated Container (1:1, never shared)
└── Has run history and statistics

CONTAINER
├── Is dedicated to one Farm (permanent binding)
├── Maintains game auth state (cannot be swapped)
├── Can be: Running, Stopped, Error
└── Lives on a specific Worker Host

CREDIT TRANSACTION
├── Purchase (Stripe)
├── Usage (daily deduction per farm)
├── Bonus (referral, trial)
└── Refund

INTEL (future feature)
├── Shield status of scanned players
├── Coordinates
├── Timestamps
└── Aggregated from Eco scans across user base
```

### Farm Modes

| Mode | Execution Pattern | User Value | Margin |
|------|-------------------|------------|--------|
| **Eco** | 5 min/hour, scheduled | "It works" | Very High |
| **Active** | 15 min/hour, scheduled | "Misses less" | High |
| **24/7** | Continuous | "Always optimal" | Medium |
| **24/7 + Reactive** | Continuous + event detection | "Maximum gains" | Medium |

---

## Critical Architectural Constraints

### Constraint 1: One Container Per Farm (Non-Negotiable)

```
WHY:
- Game auth state lives in the Android container
- Login tokens, session cookies, device fingerprints
- Cannot be transferred or shared between containers
- Re-login is expensive and may trigger game security

IMPLICATION:
- Every Farm needs a dedicated container
- Container can be stopped but NOT reassigned
- Eco mode = stop container, not release it
- Scaling = more containers, not container pooling
```

### Constraint 2: Memory is the Limiting Factor

```
WHY:
- Android emulation is RAM-hungry (~400-600MB per instance)
- A 16GB server can run ~25-30 instances max
- CPU is rarely the bottleneck

IMPLICATION:
- Worker code must be minimal footprint
- No heavy frameworks on worker nodes
- Bot logic should be as lean as possible
- Business logic lives elsewhere, not on workers
```

### Constraint 3: UI and Bot Execution Must Be Decoupled

```
WHY:
- Different scaling characteristics
- UI: scales by user count, stateless, horizontal
- Workers: scales by active farms, stateful, resource-bound
- Different failure modes (UI crash ≠ bot crash)
- Different deployment cadence

IMPLICATION:
- Separate services, separate hosts (at scale)
- Communication via queue/message bus
- Worker knows nothing about HTTP/UI
- Worker only knows: "run this farm, report result"
```

### Constraint 4: Scheduling is Business Logic, Not Worker Logic

```
WHY:
- "When to run" depends on: mode, credits, features, server load
- This is business policy, not execution concern
- Must be changeable without touching workers

IMPLICATION:
- Central scheduler service
- Workers pull jobs from queue
- Workers don't decide when to run
- Workers don't know about user tiers or credits
```

---

## System Boundaries

```
┌─────────────────────────────────────────────────────────────────┐
│                        BUSINESS DOMAIN                          │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │   Auth   │  │ Credits  │  │  Farms   │  │Scheduler │       │
│  │          │  │          │  │          │  │          │       │
│  │ Login    │  │ Purchase │  │ Create   │  │ Queue    │       │
│  │ OAuth    │  │ Deduct   │  │ Configure│  │ Jobs     │       │
│  │ Users    │  │ Balance  │  │ Status   │  │ Priority │       │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Job Queue (async, decoupled)
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        EXECUTION DOMAIN                         │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    WORKER HOST                            │  │
│  │                                                           │  │
│  │   ┌───────────┐ ┌───────────┐ ┌───────────┐             │  │
│  │   │ Container │ │ Container │ │ Container │  ...        │  │
│  │   │  Farm A   │ │  Farm B   │ │  Farm C   │             │  │
│  │   └───────────┘ └───────────┘ └───────────┘             │  │
│  │                                                           │  │
│  │   Worker Agent: receives jobs, manages containers,        │  │
│  │                 reports results. Nothing else.           │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Capacity Planning Model

### Per-Server Math

```
GIVEN:
- Server RAM: 16 GB
- Usable for containers: ~14 GB (OS + services overhead)
- RAM per container: ~500 MB
- Max parallel containers: ~28

ECO MODE (5 min runtime per hour):
- Utilization: 5/60 = 8.3%
- One slot serves: 1 / 0.083 = ~12 farms (time-sliced)
- BUT: Containers can't be shared (auth state)
- SO: 28 containers, each runs 5 min, sleeps 55 min
- Effective: 28 Eco farms per server

24/7 MODE:
- Utilization: 100%
- One container = one farm
- Effective: 28 dedicated farms per server

MIXED:
- 10 dedicated 24/7 farms (always running)
- 18 Eco farms (start/stop scheduled)
- Works because Eco containers are stopped most of the time
- Stopped containers use minimal RAM
```

### Scaling Triggers

```
ADD WORKER SERVER WHEN:
- Active (running) containers approaching host limit
- Job queue wait time exceeding threshold
- Scheduled jobs can't find available host

SCALE IS LINEAR:
- 1 server = ~28 active containers
- 2 servers = ~56 active containers
- Cost scales with active users, not registered users
```

---

## Job Execution Model

### Eco/Active Mode (Scheduled)

```
SCHEDULER (runs hourly):
1. Query: all farms in Eco/Active mode with sufficient credits
2. For each farm:
   - Calculate execution window (spread across hour)
   - Enqueue job with scheduled time
3. Done

WORKER AGENT (continuous):
1. Poll queue for jobs ready to execute
2. For each job:
   - Ensure container exists (create if first run)
   - Start container (if stopped)
   - Wait for Android boot
   - Execute bot cycle
   - Report result to business layer
   - Stop container (Eco) or keep running (Active keeps warm)
3. Loop

JOB MESSAGE:
{
  farm_id: uuid,
  mode: "eco",
  execution_type: "scheduled_cycle",
  credentials_ref: "vault://farms/{id}/creds"  // Don't pass creds directly
}

RESULT MESSAGE:
{
  farm_id: uuid,
  success: bool,
  duration_ms: int,
  resources_collected: {...},
  error: string | null,
  timestamp: datetime
}
```

### 24/7 Mode (Continuous)

```
DIFFERENT PATTERN:
- No job queue for regular cycles
- Worker runs continuous loop internally
- Only "start" and "stop" are queued commands

START 24/7:
1. User upgrades farm to 24/7
2. Scheduler checks: slot available?
3. Yes: Enqueue "start_dedicated" job
4. Worker: start container, run continuous loop
5. Loop runs indefinitely until stop signal

STOP 24/7:
1. User downgrades or runs out of credits
2. Enqueue "stop_dedicated" job
3. Worker: graceful shutdown, stop container
4. Farm returns to Eco scheduling
```

---

## Payment & Credits Flow

### Credit Purchase

```
USER                    FRONTEND                 BACKEND                  STRIPE
 │                         │                        │                        │
 │  "Buy 100 credits"      │                        │                        │
 │ ────────────────────►   │                        │                        │
 │                         │  Create checkout       │                        │
 │                         │ ─────────────────────► │                        │
 │                         │                        │  Create session        │
 │                         │                        │ ─────────────────────► │
 │                         │                        │                        │
 │                         │        Checkout URL    │ ◄───────────────────── │
 │                         │ ◄───────────────────── │                        │
 │   Redirect to Stripe    │                        │                        │
 │ ◄─────────────────────  │                        │                        │
 │                         │                        │                        │
 │   [User pays]           │                        │                        │
 │                         │                        │                        │
 │                         │                        │   Webhook: success     │
 │                         │                        │ ◄───────────────────── │
 │                         │                        │                        │
 │                         │                        │  Add credits to user   │
 │                         │                        │  Log transaction       │
 │                         │                        │                        │
 │   Redirect to success   │                        │                        │
 │ ◄─────────────────────────────────────────────────────────────────────── │
```

### Credit Deduction

```
DAILY JOB (runs at midnight):

For each active farm:
1. Calculate daily cost based on mode
2. Check user credit balance
3. If sufficient:
   - Deduct credits
   - Log transaction
4. If insufficient:
   - Downgrade farm to lower mode (or pause)
   - Notify user
   - Log event

COST TABLE:
- Eco: 2 credits/day
- Active: 5 credits/day
- 24/7: 12 credits/day
- 24/7 + Reactive: 18 credits/day

Note: Costs are configurable, not hardcoded.
```

---

## User Onboarding Flow

```
1. REGISTRATION
   - OAuth (Discord preferred for gamer demographic)
   - Create user record
   - Grant trial credits (e.g., 100 credits)
   - Generate referral code

2. FIRST FARM CREATION
   - User provides: game account credentials, server selection
   - System: creates Farm record, creates Container (not started)
   - System: queues validation job

3. FARM VALIDATION
   - Worker: starts container, attempts game login
   - Success: mark farm active, grant trial premium (7 days 24/7)
   - Failure: mark farm error, notify user with reason

4. FIRST RUN
   - Trial premium: farm runs in 24/7 mode
   - User experiences full value immediately
   - After trial: falls back to Eco (hooks them)

5. CONVERSION
   - User buys credits to maintain premium
   - Or stays on Eco (still engaged, still revenue)
```

---

## Multi-Farm & Upselling

```
EXPANSION TRIGGERS:
- User sees: "With 2 farms you'd have 2x output"
- User sees: "Top users average 3.2 farms"
- New farm bonus: 3 days premium trial

PREMIUM FEATURE UPSELLS:
- Real-time analytics dashboard
- Reactive micro-event capture
- Shield-down alerts (intel feature)
- Alliance coordination tools

PSYCHOLOGICAL HOOKS:
- Show "missed opportunity" counters
- Limited 24/7 slots (real scarcity)
- Bulk credit discounts
- Referral bonuses
```

---

## Future: Intel Network

```
THE VISION:
- Every Eco farm scans surroundings during its run
- Shield status, resource estimates, activity timestamps
- Aggregated into shared intel database
- Sold as premium feature to whale tier

ECONOMICS:
- Eco users pay with data (unknowingly)
- Whales pay for aggregated intel
- Network effect: more Eco users = better intel = more whale value

PRIVACY CONSIDERATIONS:
- No personal data collected
- Only in-game public information
- Users consent in ToS to "network features"
```

---

## Infrastructure Principles

### MVP (Single Server)

```
- Everything on one machine is fine initially
- Logical separation (containers) even if same host
- Design for split, deploy as monolith
- Database backup is critical (user data, credits)
- Game auth state backup is critical (container volumes)
```

### Growth Path

```
TRIGGER: Worker capacity becoming bottleneck

STEP 1: Separate worker host
- Business stack stays on host A
- Workers move to host B (more RAM)
- Connected via queue

STEP 2: Multiple worker hosts
- Load balance farm assignments
- Sticky assignment (farm stays on same host)
- Host failure = those farms need re-setup

STEP 3: Proper orchestration
- Evaluate: Proxmox cluster, K8s, etc.
- Based on actual pain points, not theoretical
```

---

## Non-Functional Requirements

### Performance

```
- Bot cycle (Eco run): < 5 minutes
- Container cold start: < 60 seconds
- Job queue latency: < 30 seconds
- API response time: < 200ms (p95)
```

### Reliability

```
- Bot failure: retry 2x, then mark error, notify user
- Container crash: auto-restart, log event
- Host failure: manual intervention (MVP), auto-failover (later)
- Payment webhook: idempotent, retry-safe
```

### Security

```
- Game credentials: encrypted at rest, never logged
- User passwords: hashed, standard practices
- API: authenticated, authorized by user/farm ownership
- Worker: no direct external access, only queue
```

---

## What This Document is NOT

```
- NOT a tech stack decision (that's implementation)
- NOT a database schema (derive from domain model)
- NOT an API specification (derive from flows)
- NOT a deployment guide (separate concern)

This document answers:
- WHAT are we building?
- WHY these architectural constraints?
- HOW do the parts relate?

Implementation answers:
- Which framework?
- Which database?
- Which message queue?
```

---

## Key Decisions Summary

| Decision | Rationale |
|----------|-----------|
| 1:1 Container:Farm binding | Auth state cannot be shared |
| Decouple UI from Workers | Different scaling, failure modes |
| Centralized scheduling | Business logic belongs in business layer |
| Lean workers | Memory is the constraint |
| Credits not subscriptions | Flexibility, lower commitment barrier |
| Eco as default | High margin, capacity efficient |
| 24/7 as limited premium | Real scarcity, whale monetization |
| Trial premium on first farm | Hook users with full experience |

---

## For Your Dev Agent

```
REMEMBER:
- UI service knows nothing about containers
- Worker service knows nothing about users or payments
- Scheduler decides WHEN, Worker decides HOW
- One farm = one container, forever bound
- Container stop ≠ release, it's just paused
- Memory is king on workers, keep them thin
- Queue is the integration point, keep it simple
- Credentials are sacred, encrypt everything
- Design for split, deploy as needed
```

---

*This document should be the source of truth for architectural decisions. Implementation details are negotiable, these constraints are not.*