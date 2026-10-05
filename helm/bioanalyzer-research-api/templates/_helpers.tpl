{{/*
Expand the name of the chart.
*/}}
{{- define "bioanalyzer-research-api.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
Use the release name as the primary identifier to avoid double-naming.
*/}}
{{- define "bioanalyzer-research-api.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "bioanalyzer-research-api.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "bioanalyzer-research-api.labels" -}}
helm.sh/chart: {{ include "bioanalyzer-research-api.chart" . }}
{{ include "bioanalyzer-research-api.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- with .Values.commonLabels }}
{{ toYaml . }}
{{- end }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "bioanalyzer-research-api.selectorLabels" -}}
app.kubernetes.io/name: {{ include "bioanalyzer-research-api.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "bioanalyzer-research-api.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "bioanalyzer-research-api.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Create the name of the configmap
*/}}
{{- define "bioanalyzer-research-api.configmapName" -}}
{{- printf "%s-config" (include "bioanalyzer-research-api.fullname" .) }}
{{- end }}

{{/*
Create the name of the secret
*/}}
{{- define "bioanalyzer-research-api.secretName" -}}
{{- if .Values.secrets.azure.existingSecret }}
{{- .Values.secrets.azure.existingSecret }}
{{- else }}
{{- printf "%s-secrets" (include "bioanalyzer-research-api.fullname" .) }}
{{- end }}
{{- end }}

{{/*
Common annotations
*/}}
{{- define "bioanalyzer-research-api.annotations" -}}
{{- with .Values.commonAnnotations }}
{{ toYaml . }}
{{- end }}
{{- end }}