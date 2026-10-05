function extractSsePayload(raw: string): string {
  if (!raw.includes('data:') && !raw.includes('event:')) {
    return raw;
  }

  const lines = raw.split(/\r?\n/);
  const extracted: string[] = [];
  let hasDataLines = false;

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) {
      continue;
    }

    if (trimmed.startsWith('event:')) {
      continue;
    }

    if (trimmed.startsWith('data:')) {
      hasDataLines = true;
      const payload = trimmed.slice('data:'.length).trimStart();
      if (!payload || payload === '[DONE]') {
        continue;
      }

      try {
        const parsed = JSON.parse(payload);
        extracted.push(typeof parsed === 'string' ? parsed : String(parsed));
      } catch {
        extracted.push(payload);
      }
      continue;
    }

    extracted.push(line);
  }

  return hasDataLines ? extracted.join('\n') : raw;
}

export function formatAssistantResponse(raw: string): string {
  const withoutTransport = extractSsePayload(raw);
  return withoutTransport
    .replace(/\s+\n/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .replace(/\s*\[Source\s+(\d+)\]/g, ' [Source $1]')
    .trim();
}
