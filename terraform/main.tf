terraform {
  required_version = ">= 1.5.0"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.20"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

# -------------------------------------------------------------------------------------------------
# 1. IAM & SERVICE ACCOUNT
# -------------------------------------------------------------------------------------------------
resource "google_service_account" "nexus_sa" {
  account_id   = "nexus-extract-sa"
  display_name = "Nexus Extract Gateway Service Identity"
  description  = "Least-privilege workload identity for Nexus API and streaming Worker"
}

# IAM Roles for PubSub, Storage, Trace, and Cloud SQL
resource "google_project_iam_member" "pubsub_publisher" {
  project = var.project_id
  role    = "roles/pubsub.publisher"
  member  = "serviceAccount:${google_service_account.nexus_sa.email}"
}

resource "google_project_iam_member" "pubsub_subscriber" {
  project = var.project_id
  role    = "roles/pubsub.subscriber"
  member  = "serviceAccount:${google_service_account.nexus_sa.email}"
}

resource "google_project_iam_member" "gcs_admin" {
  project = var.project_id
  role    = "roles/storage.objectAdmin"
  member  = "serviceAccount:${google_service_account.nexus_sa.email}"
}

resource "google_project_iam_member" "cloudsql_client" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.nexus_sa.email}"
}

resource "google_project_iam_member" "cloud_trace" {
  project = var.project_id
  role    = "roles/cloudtrace.agent"
  member  = "serviceAccount:${google_service_account.nexus_sa.email}"
}

resource "google_project_iam_member" "metric_writer" {
  project = var.project_id
  role    = "roles/monitoring.metricWriter"
  member  = "serviceAccount:${google_service_account.nexus_sa.email}"
}

# -------------------------------------------------------------------------------------------------
# 2. GOOGLE CLOUD STORAGE (High-Volume Data Lake Sink)
# -------------------------------------------------------------------------------------------------
resource "google_storage_bucket" "datalake" {
  name                        = "nexus-datalake-${var.project_id}-${var.environment}"
  location                    = var.region
  storage_class               = "STANDARD"
  uniform_bucket_level_access = true

  versioning {
    enabled = true
  }

  lifecycle_rule {
    condition {
      age = 90
    }
    action {
      type          = "SetStorageClass"
      storage_class = "NEARLINE"
    }
  }

  lifecycle_rule {
    condition {
      age = 365
    }
    action {
      type = "Delete"
    }
  }
}

# -------------------------------------------------------------------------------------------------
# 3. GOOGLE PUB/SUB (Resilient Messaging, Retries & Dead-Letter Queue)
# -------------------------------------------------------------------------------------------------
# Main Topic for Extraction Jobs
resource "google_pubsub_topic" "extraction_jobs" {
  name = "extraction-jobs"

  message_retention_duration = "604800s" # 7 days
}

# Dead-Letter Topic (DLQ) for exhausted retry jobs
resource "google_pubsub_topic" "extraction_jobs_dlq" {
  name = "extraction-jobs-dlq"

  message_retention_duration = "1209600s" # 14 days
}

# Status Event Topic for UI and audit consumers
resource "google_pubsub_topic" "job_events" {
  name = "job-events"

  message_retention_duration = "259200s" # 3 days
}

# Worker Subscription with Exponential Dead-Letter Policy
resource "google_pubsub_subscription" "worker_subscription" {
  name  = "nexus-worker-sub"
  topic = google_pubsub_topic.extraction_jobs.name

  ack_deadline_seconds       = 300 # 5 minutes for large cursor batches
  retain_acked_messages      = false
  message_retention_duration = "604800s"

  dead_letter_policy {
    dead_letter_topic     = google_pubsub_topic.extraction_jobs_dlq.id
    max_delivery_attempts = 3
  }

  retry_policy {
    minimum_backoff = "10s"
    maximum_backoff = "600s"
  }
}

# -------------------------------------------------------------------------------------------------
# 4. GOOGLE CLOUD SQL (SQL Server 2022 Instance)
# -------------------------------------------------------------------------------------------------
resource "google_sql_database_instance" "sqlserver" {
  name             = "nexus-sqlserver-${var.environment}"
  database_version = "SQLSERVER_2022_STANDARD"
  region           = var.region

  settings {
    tier              = var.db_instance_tier
    availability_type = var.environment == "prod" ? "REGIONAL" : "ZONAL"
    disk_size         = 100
    disk_type         = "PD_SSD"
    disk_autoresize   = true

    backup_configuration {
      enabled    = true
      start_time = "02:00"
    }

    ip_configuration {
      ipv4_enabled    = true
      private_network = null
    }

    database_flags {
      name  = "1204" # Sybase/SQL Server deadlock trace flag
      value = "on"
    }
  }

  deletion_protection = var.environment == "prod"
}

resource "google_sql_user" "db_user" {
  instance = google_sql_database_instance.sqlserver.name
  name     = "nexusadmin"
  password = var.db_password
}

# -------------------------------------------------------------------------------------------------
# 5. SECRET MANAGER (Credentials & Connection Strings)
# -------------------------------------------------------------------------------------------------
resource "google_secret_manager_secret" "db_conn_str" {
  secret_id = "nexus-db-connection-string"
  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "db_conn_str_v1" {
  secret      = google_secret_manager_secret.db_conn_str.id
  secret_data = "Server=${google_sql_database_instance.sqlserver.public_ip_address};Database=NexusExtractDb;User Id=nexusadmin;Password=${var.db_password};TrustServerCertificate=True;MultipleActiveResultSets=True;"
}

# -------------------------------------------------------------------------------------------------
# 6. GOOGLE CLOUD RUN (Nexus API Service)
# -------------------------------------------------------------------------------------------------
resource "google_cloud_run_v2_service" "api_service" {
  name     = "nexus-extract-api"
  location = var.region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.nexus_sa.email

    scaling {
      min_instance_count = 1
      max_instance_count = 10
    }

    containers {
      image = var.api_container_image

      resources {
        limits = {
          cpu    = "2"
          memory = "2Gi"
        }
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name  = "ConnectionStrings__SqlServerConnection"
        value = "Server=${google_sql_database_instance.sqlserver.public_ip_address};Database=NexusExtractDb;User Id=nexusadmin;Password=${var.db_password};TrustServerCertificate=True;MultipleActiveResultSets=True;"
      }
      env {
        name  = "GcpPubSub__ProjectId"
        value = var.project_id
      }
      env {
        name  = "CloudStorage__BucketName"
        value = google_storage_bucket.datalake.name
      }
      env {
        name  = "OTEL_EXPORTER_OTLP_ENDPOINT"
        value = "http://otel-collector:4317"
      }

      ports {
        container_port = 8080
      }
    }
  }
}

# Allow Unauthenticated Public Ingress to API (protected by API Key / Bearer Tokens in middleware)
resource "google_cloud_run_service_iam_member" "public_api_access" {
  location = google_cloud_run_v2_service.api_service.location
  service  = google_cloud_run_v2_service.api_service.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}

# -------------------------------------------------------------------------------------------------
# 7. GOOGLE CLOUD RUN (Nexus Worker Service)
# -------------------------------------------------------------------------------------------------
resource "google_cloud_run_v2_service" "worker_service" {
  name     = "nexus-extract-worker"
  location = var.region
  ingress  = "INGRESS_TRAFFIC_INTERNAL_ONLY"

  template {
    service_account = google_service_account.nexus_sa.email

    scaling {
      min_instance_count = 1
      max_instance_count = 20
    }

    containers {
      image = var.worker_container_image

      resources {
        limits = {
          cpu    = "4"
          memory = "4Gi"
        }
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name  = "ConnectionStrings__SqlServerConnection"
        value = "Server=${google_sql_database_instance.sqlserver.public_ip_address};Database=NexusExtractDb;User Id=nexusadmin;Password=${var.db_password};TrustServerCertificate=True;MultipleActiveResultSets=True;"
      }
      env {
        name  = "GcpPubSub__ProjectId"
        value = var.project_id
      }
      env {
        name  = "CloudStorage__BucketName"
        value = google_storage_bucket.datalake.name
      }
    }
  }
}
