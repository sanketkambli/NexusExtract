variable "project_id" {
  description = "The Google Cloud Platform project ID."
  type        = string
  default     = "nexus-extract-prod"
}

variable "region" {
  description = "The GCP region to deploy resources into."
  type        = string
  default     = "us-central1"
}

variable "environment" {
  description = "Deployment environment (e.g. dev, staging, prod)."
  type        = string
  default     = "prod"
}

variable "db_instance_tier" {
  description = "Compute tier for Cloud SQL Server instance."
  type        = string
  default     = "db-custom-2-7680"
}

variable "db_password" {
  description = "Administrator password for Cloud SQL Server instance."
  type        = string
  sensitive   = true
  default     = "N3xus!Str0ngP@ss2026"
}

variable "api_container_image" {
  description = "Artifact Registry container image URI for NexusExtract.Api."
  type        = string
  default     = "us-central1-docker.pkg.dev/nexus-extract-prod/nexus-repo/nexus-api:latest"
}

variable "worker_container_image" {
  description = "Artifact Registry container image URI for NexusExtract.Worker."
  type        = string
  default     = "us-central1-docker.pkg.dev/nexus-extract-prod/nexus-repo/nexus-worker:latest"
}
