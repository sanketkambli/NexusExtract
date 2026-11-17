output "api_service_url" {
  description = "The public URL of the deployed NexusExtract.Api Cloud Run service."
  value       = google_cloud_run_v2_service.api_service.uri
}

output "datalake_bucket_name" {
  description = "The Google Cloud Storage bucket name for high-volume streaming data lake ingestion."
  value       = google_storage_bucket.datalake.name
}

output "pubsub_extraction_jobs_topic" {
  description = "The Pub/Sub topic ID for incoming extraction jobs."
  value       = google_pubsub_topic.extraction_jobs.id
}

output "pubsub_dlq_topic" {
  description = "The Pub/Sub Dead-Letter Queue (DLQ) topic ID for failed extraction jobs."
  value       = google_pubsub_topic.extraction_jobs_dlq.id
}

output "cloudsql_instance_connection_name" {
  description = "The connection name of the Cloud SQL Server instance for Cloud SQL Auth Proxy."
  value       = google_sql_database_instance.sqlserver.connection_name
}

output "service_account_email" {
  description = "The service account email running the Nexus workloads."
  value       = google_service_account.nexus_sa.email
}
