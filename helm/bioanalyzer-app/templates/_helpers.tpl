{{/*
Expand the name of the chart.
*/}}
{{- define "bioanalyzer-app.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
Use the release name as the primary identifier to avoid double-naming.
*/}}
{{- define "bioanalyzer-app.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "bioanalyzer-app.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "bioanalyzer-app.labels" -}}
helm.sh/chart: {{ include "bioanalyzer-app.chart" . }}
{{ include "bioanalyzer-app.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "bioanalyzer-app.selectorLabels" -}}
app.kubernetes.io/name: {{ include "bioanalyzer-app.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "bioanalyzer-app.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "bioanalyzer-app.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Create the name of the configmap
*/}}
{{- define "bioanalyzer-app.configMapName" -}}
{{- printf "%s-config" (include "bioanalyzer-app.fullname" .) }}
{{- end }}