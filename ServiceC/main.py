import asyncio
import logging
from datetime import datetime
from fastapi import FastAPI
from dapr.ext.fastapi import DaprApp
from dapr.clients import DaprClient
from cloudevents.http import CloudEvent
from models import DataProcessingRequest, ProcessingProgress, ProcessingComplete

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='[ServiceC] %(asctime)s - %(levelname)s - %(message)s',
    datefmt='%H:%M:%S'
)
logger = logging.getLogger(__name__)

app = FastAPI(title="ServiceC - Data Processing Service")
dapr_app = DaprApp(app)

# Constants
PUBSUB_NAME = "pubsub"
PROGRESS_TOPIC = "servicec-progress"
COMPLETE_TOPIC = "servicec-complete"

# Processing steps configuration
PROCESSING_STEPS = [
    {"name": "LoadingData", "duration": 5, "message": "Loading data from source systems"},
    {"name": "ValidatingSchema", "duration": 4, "message": "Validating data schema and formats"},
    {"name": "TransformingData", "duration": 6, "message": "Applying data transformations"},
    {"name": "EnrichingRecords", "duration": 5, "message": "Enriching records with external data"},
    {"name": "AggregatingResults", "duration": 4, "message": "Aggregating and summarizing results"},
    {"name": "PersistingOutput", "duration": 3, "message": "Persisting output to storage"},
]


async def publish_progress(
    correlation_id: str,
    workflow_instance_id: str,
    step_name: str,
    step_number: int,
    total_steps: int,
    percent_complete: int,
    message: str
):
    """Publish a progress update to ServiceB"""
    # Use camelCase to match C# JSON conventions
    progress_data = {
        "correlationId": correlation_id,
        "workflowInstanceId": workflow_instance_id,
        "stepName": step_name,
        "stepNumber": step_number,
        "totalSteps": total_steps,
        "percentComplete": percent_complete,
        "message": message,
        "timestamp": datetime.utcnow().isoformat()
    }
    
    import json
    with DaprClient() as client:
        client.publish_event(
            pubsub_name=PUBSUB_NAME,
            topic_name=PROGRESS_TOPIC,
            data=json.dumps(progress_data),
            data_content_type="application/json"
        )
    
    logger.info(f"[Progress] {percent_complete}% - {step_name}: {message}")


async def publish_completion(
    correlation_id: str,
    workflow_instance_id: str,
    success: bool,
    message: str,
    duration: float
):
    """Publish completion event to ServiceB"""
    # Use camelCase to match C# JSON conventions
    complete_data = {
        "correlationId": correlation_id,
        "workflowInstanceId": workflow_instance_id,
        "success": success,
        "message": message,
        "totalDurationSeconds": duration,
        "completedAt": datetime.utcnow().isoformat()
    }
    
    import json
    with DaprClient() as client:
        client.publish_event(
            pubsub_name=PUBSUB_NAME,
            topic_name=COMPLETE_TOPIC,
            data=json.dumps(complete_data),
            data_content_type="application/json"
        )
    
    logger.info(f"[Complete] Success={success}, Duration={duration:.2f}s - {message}")


async def process_data(request: DataProcessingRequest):
    """Main data processing logic with progress updates"""
    start_time = datetime.utcnow()
    total_steps = len(PROCESSING_STEPS)
    
    logger.info("=" * 60)
    logger.info(f"Starting Data Processing")
    logger.info(f"  Correlation ID: {request.correlation_id}")
    logger.info(f"  Workflow Instance: {request.workflow_instance_id}")
    logger.info(f"  Requested By: {request.requested_by}")
    logger.info(f"  Total Steps: {total_steps}")
    logger.info("=" * 60)
    
    try:
        for i, step in enumerate(PROCESSING_STEPS, 1):
            step_name = step["name"]
            duration = step["duration"]
            message = step["message"]
            
            # Calculate progress
            # Start of step
            start_percent = int(((i - 1) / total_steps) * 100)
            
            # Publish "starting step" progress
            await publish_progress(
                correlation_id=request.correlation_id,
                workflow_instance_id=request.workflow_instance_id,
                step_name=step_name,
                step_number=i,
                total_steps=total_steps,
                percent_complete=start_percent,
                message=f"Starting: {message}"
            )
            
            # Simulate work with intermediate progress
            intervals = 4
            for j in range(intervals):
                await asyncio.sleep(duration / intervals)
                
                # Calculate intermediate progress within this step
                step_progress = ((j + 1) / intervals)
                current_percent = int(((i - 1 + step_progress) / total_steps) * 100)
                
                if j < intervals - 1:  # Don't publish at 100% of step, we'll do that separately
                    await publish_progress(
                        correlation_id=request.correlation_id,
                        workflow_instance_id=request.workflow_instance_id,
                        step_name=step_name,
                        step_number=i,
                        total_steps=total_steps,
                        percent_complete=current_percent,
                        message=f"Processing: {message} ({int(step_progress * 100)}%)"
                    )
            
            # Publish "completed step" progress
            end_percent = int((i / total_steps) * 100)
            await publish_progress(
                correlation_id=request.correlation_id,
                workflow_instance_id=request.workflow_instance_id,
                step_name=step_name,
                step_number=i,
                total_steps=total_steps,
                percent_complete=end_percent,
                message=f"Completed: {message}"
            )
            
            logger.info(f"[Step {i}/{total_steps}] ✓ {step_name} completed")
        
        # Calculate total duration
        end_time = datetime.utcnow()
        duration_seconds = (end_time - start_time).total_seconds()
        
        # Publish completion
        await publish_completion(
            correlation_id=request.correlation_id,
            workflow_instance_id=request.workflow_instance_id,
            success=True,
            message=f"All {total_steps} steps completed successfully",
            duration=duration_seconds
        )
        
        logger.info("=" * 60)
        logger.info(f"Data Processing COMPLETED")
        logger.info(f"  Total Duration: {duration_seconds:.2f} seconds")
        logger.info("=" * 60)
        
    except Exception as e:
        end_time = datetime.utcnow()
        duration_seconds = (end_time - start_time).total_seconds()
        
        logger.error(f"Data Processing FAILED: {str(e)}")
        
        await publish_completion(
            correlation_id=request.correlation_id,
            workflow_instance_id=request.workflow_instance_id,
            success=False,
            message=f"Processing failed: {str(e)}",
            duration=duration_seconds
        )


@dapr_app.subscribe(pubsub=PUBSUB_NAME, topic="servicec-request", route="/servicec-request")
async def handle_processing_request(event: dict):
    """Handle incoming data processing requests from ServiceB"""
    logger.info("=" * 60)
    logger.info("Received Data Processing Request")
    logger.info("=" * 60)
    
    try:
        # Parse the request - event is already a dict with CloudEvent structure
        data = event.get("data", event)
        if isinstance(data, str):
            import json
            data = json.loads(data)
        
        request = DataProcessingRequest(
            correlation_id=data.get("correlationId", data.get("correlation_id", "")),
            workflow_instance_id=data.get("workflowInstanceId", data.get("workflow_instance_id", "")),
            requested_by=data.get("requestedBy", data.get("requested_by", "Unknown")),
            requested_at=data.get("requestedAt", data.get("requested_at", datetime.utcnow().isoformat()))
        )
        
        # Fire and forget - start processing in background
        asyncio.create_task(process_data(request))
        
        logger.info(f"Processing task started for correlation: {request.correlation_id}")
        
        return {"status": "ACCEPTED"}
        
    except Exception as e:
        logger.error(f"Error handling request: {str(e)}")
        return {"status": "ERROR", "message": str(e)}


@app.get("/health")
async def health():
    """Health check endpoint"""
    return {
        "status": "Healthy",
        "service": "ServiceC",
        "timestamp": datetime.utcnow().isoformat()
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=5003)
