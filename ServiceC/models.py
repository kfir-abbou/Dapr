from pydantic import BaseModel
from datetime import datetime
from typing import Optional, Any, Dict

class DataProcessingRequest(BaseModel):
    """Request from ServiceB to start data processing"""
    correlation_id: str
    workflow_instance_id: str
    requested_by: str = "Unknown"
    requested_at: Optional[str] = None
    data: Optional[Dict[str, Any]] = None

class ProcessingProgress(BaseModel):
    """Progress update sent back to ServiceB"""
    correlation_id: str
    workflow_instance_id: str
    step_name: str
    step_number: int
    total_steps: int
    percent_complete: int
    message: str
    timestamp: datetime

class ProcessingComplete(BaseModel):
    """Completion event sent back to ServiceB"""
    correlation_id: str
    workflow_instance_id: str
    success: bool
    message: str
    total_duration_seconds: float
    completed_at: datetime
