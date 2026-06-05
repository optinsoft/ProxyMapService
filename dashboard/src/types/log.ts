export interface LogEntry {
  timestamp: string;
  category: string;
  level: 'Debug' | 'Information' | 'Warning' | 'Error' | 'Critical';
  message: string;
  exception?: string | null;
}

export interface HttpRequestEntry {
  timestamp: string;
  id: string;
  method: string;
  target: string;
  route: string;
  headers: Record<string, string>;
}

export interface HttpResponseEntry {
  timestamp: string;
  id: string;
  statusCode: string;
  statusText: string;
  route: string;
  headers: Record<string, string>;
}

// Unified state for display row mapping
export interface MergedTrafficEntry {
  id: string;
  timestamp: string;
  method: string;
  target: string;
  route: string;
  statusCode: number | null;
  statusText: string;
  durationMs: number | null;
  requestHeaders: Record<string, string>;
  responseHeaders: Record<string, string>;
}