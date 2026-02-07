# System Architecture

## Overview Diagram

```mermaid
flowchart TB
    subgraph Client["🖥️ Client"]
        API["HTTP Requests"]
    end

    subgraph ServiceA["ServiceA (.NET 8.0)"]
        direction TB
        A_Port["Port: 5001<br/>Dapr: 3500"]
        A_Endpoints["Endpoints:<br/>POST /batch/start<br/>GET /batch/status/{id}"]
        A_Subscriber["Subscriber:<br/>/events/batch-response"]
        A_Workflow["SetupWorkflow"]
        A_Backup["StateBackupService"]
    end

    subgraph Redis["🔴 Redis (localhost:6379)"]
        StateStore["State Store<br/>(state.redis)"]
        PubSub["Pub/Sub Broker<br/>(pubsub.redis)"]
    end

    subgraph ServiceB_Cluster["ServiceB Cluster (.NET 8.0)"]
        direction TB
        subgraph SB1["ServiceB-1"]
            B1_Port["Port: 5002<br/>Dapr: 3502"]
        end
        subgraph SB2["ServiceB-2"]
            B2_Port["Port: 5012<br/>Dapr: 3512"]
        end
        subgraph SB3["ServiceB-3"]
            B3_Port["Port: 5022<br/>Dapr: 3522"]
        end
        B_Workflow["BatchOrchestratorWorkflow<br/>5 Parallel Tasks"]
    end

    subgraph ServiceC["ServiceC (Python FastAPI)"]
        direction TB
        C_Port["Port: 5003<br/>Dapr: 3503"]
        C_Process["Data Processing<br/>6 Steps with Progress"]
    end

    %% Client to ServiceA
    API -->|"POST /batch/start"| A_Endpoints

    %% ServiceA publishes to Redis
    A_Endpoints -->|"Publish"| PubSub

    %% Redis to ServiceB (competing consumers)
    PubSub -->|"batch-process-request<br/>(any instance)"| ServiceB_Cluster

    %% ServiceB workflow activities
    B_Workflow -->|"Publish<br/>servicec-request"| PubSub
    PubSub -->|"servicec-request"| C_Process

    %% ServiceC responses
    C_Process -->|"Publish<br/>servicec-progress"| PubSub
    C_Process -->|"Publish<br/>servicec-complete"| PubSub
    PubSub -->|"External Event"| B_Workflow

    %% ServiceB publishes response
    ServiceB_Cluster -->|"Publish<br/>batch-process-response"| PubSub
    PubSub -->|"batch-process-response"| A_Subscriber

    %% State store interactions
    A_Subscriber -->|"Save Response"| StateStore
    A_Backup -->|"Backup State"| StateStore
    B_Workflow -->|"Workflow State"| StateStore

    %% Status check
    API -->|"GET /batch/status/{id}"| A_Endpoints
    A_Endpoints -->|"Read Response"| StateStore
```

## Detailed Pub/Sub Topics

```mermaid
flowchart LR
    subgraph Topics["📨 Pub/Sub Topics"]
        T1["batch-process-request"]
        T2["batch-process-response"]
        T3["servicec-request"]
        T4["servicec-progress"]
        T5["servicec-complete"]
    end

    SA["ServiceA"] -->|publish| T1
    T1 -->|subscribe| SB["ServiceB<br/>(3 instances)"]
    
    SB -->|publish| T2
    T2 -->|subscribe| SA

    SB -->|publish| T3
    T3 -->|subscribe| SC["ServiceC"]

    SC -->|publish| T4
    SC -->|publish| T5
    T4 -->|external event| SB
    T5 -->|external event| SB
```

## BatchOrchestratorWorkflow - Parallel Tasks

```mermaid
flowchart TB
    subgraph Orchestrator["BatchOrchestratorWorkflow"]
        Start["Start Workflow"]
        
        subgraph Parallel["⚡ Task.WhenAll (5 Parallel Tasks)"]
            direction LR
            T1["🔄 ServiceC<br/>Data Processing<br/>(fire-and-forget + wait)"]
            T2["✅ Validation<br/>Activity<br/>(2s delay)"]
            T3["📊 Enrichment<br/>Activity<br/>(4s delay)"]
            T4["📧 Notification<br/>Activity<br/>(1.5s delay)"]
            T5["👤 Approval<br/>External Event<br/>(3 min timeout)"]
        end
        
        Collect["Collect Results"]
        Complete["Return BatchOrchestratorOutput"]
    end

    Start --> Parallel
    T1 & T2 & T3 & T4 & T5 --> Collect
    Collect --> Complete
```

## ServiceC Data Processing Flow

```mermaid
sequenceDiagram
    participant SB as ServiceB
    participant PS as Pub/Sub (Redis)
    participant SC as ServiceC

    SB->>PS: Publish to servicec-request
    PS->>SC: Deliver request
    SC-->>SC: Start processing (6 steps)
    
    loop Each Step (1-6)
        SC->>PS: Publish progress update
        PS->>SB: servicec-progress event
        SC-->>SC: Simulate work (1s delay)
    end
    
    SC->>PS: Publish completion
    PS->>SB: servicec-complete event
    SB-->>SB: Resume workflow
```

## Request Flow - Complete Sequence

```mermaid
sequenceDiagram
    participant C as Client
    participant SA as ServiceA
    participant R as Redis (Pub/Sub)
    participant SB as ServiceB (any instance)
    participant SC as ServiceC

    C->>SA: POST /batch/start
    SA->>R: Publish batch-process-request
    SA-->>C: 202 Accepted (correlationId)

    R->>SB: Deliver to available instance
    SB->>SB: Start BatchOrchestratorWorkflow
    
    par 5 Parallel Tasks
        SB->>R: Publish servicec-request
        R->>SC: Deliver request
        SC->>R: Progress updates
        SC->>R: Completion event
        R->>SB: External event received
    and
        SB->>SB: Run Validation (2s)
    and
        SB->>SB: Run Enrichment (4s)
    and
        SB->>SB: Run Notification (1.5s)
    and
        SB->>SB: Wait for Approval event
        C->>SB: POST /workflow/approve
    end

    SB->>SB: Collect all results
    SB->>R: Publish batch-process-response
    R->>SA: Deliver response
    SA->>R: Save to state store

    C->>SA: GET /batch/status/{correlationId}
    SA->>R: Read from state store
    SA-->>C: Return results
```

## Component Summary

| Service | Technology | Port | Dapr Port | Role |
|---------|------------|------|-----------|------|
| ServiceA | .NET 8.0 | 5001 | 3500 | API Gateway, Workflow Orchestration |
| ServiceB-1 | .NET 8.0 | 5002 | 3502 | Batch Processing Worker |
| ServiceB-2 | .NET 8.0 | 5012 | 3512 | Batch Processing Worker |
| ServiceB-3 | .NET 8.0 | 5022 | 3522 | Batch Processing Worker |
| ServiceC | Python FastAPI | 5003 | 3503 | Data Processing Service |
| Redis | Redis 7 | 6379 | - | State Store & Pub/Sub |

## Dapr Building Blocks Used

| Building Block | Component | Usage |
|----------------|-----------|-------|
| **Pub/Sub** | pubsub.redis | Async messaging between services |
| **State Store** | state.redis | Workflow state, response storage |
| **Workflow** | Dapr.Workflow | Durable orchestration in ServiceA & ServiceB |
| **Bindings** | statebackup | File output for state backup |
