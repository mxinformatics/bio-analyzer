function isEnabled(value: string | undefined, defaultEnabled: boolean): boolean {
  if (value == null || value.trim() === '') {
    return defaultEnabled;
  }
  const normalized = value.trim().toLowerCase();
  if (['1', 'true', 'yes', 'on'].includes(normalized)) {
    return true;
  }
  if (['0', 'false', 'no', 'off'].includes(normalized)) {
    return false;
  }
  return defaultEnabled;
}

export type AgentFeatureFlags = {
  expansionToolsEnabled: boolean;
  autoIngestEnabled: boolean;
  interimAbstractsEnabled: boolean;
  /** Phase C2: agent blob download tool is off by default (HTTP App UI still allowed). */
  blobDownloadToolEnabled: boolean;
};

export function getAgentFeatureFlags(): AgentFeatureFlags {
  return {
    expansionToolsEnabled: isEnabled(process.env.EXPANSION_TOOLS_ENABLED, true),
    autoIngestEnabled: isEnabled(process.env.AUTO_INGEST_ENABLED, true),
    interimAbstractsEnabled: isEnabled(process.env.INTERIM_ABSTRACTS_ENABLED, true),
    // Default OFF — prevents agent from pulling arbitrary catalog files unless explicitly enabled.
    blobDownloadToolEnabled: isEnabled(process.env.AGENT_BLOB_DOWNLOAD_TOOL_ENABLED, false),
  };
}
