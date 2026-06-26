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
  inbound: string;
  requestURI: string;
  requestMethod: string;
  route: string;
  targetHost: string;
  headers: Record<string, string>;
}

export interface HttpResponseEntry {
  timestamp: string;
  id: string;
  inbound: string;
  statusCode: string;
  statusText: string;
  route: string;
  targetHost: string;
  headers: Record<string, string>;
}

export type HttpBodyEntry =
  | HttpJsonBodyEntry
  | HttpXmlBodyEntry
  | HttpHtmlBodyEntry
  | HttpTextBodyEntry
  | HttpImageBodyEntry
  | HttpBinaryBodyEntry
  | HttpFormUrlEncodedBodyEntry;

interface HttpBodyBaseEntry {
  id: string;
  length: number;
  contentType?: string | null;
}

export enum HttpContentKind {
  Json = 0,
  Xml = 1,
  Html = 2,
  Text = 3,
  Image = 4,
  Binary = 5,
  FormUrlEncoded = 6
}

export interface HttpJsonBodyEntry
  extends HttpBodyBaseEntry {
  contentKind: HttpContentKind.Json;
  content: string;
}

export interface HttpXmlBodyEntry
  extends HttpBodyBaseEntry {
  contentKind: HttpContentKind.Xml;
  content: string;
}

export interface HttpHtmlBodyEntry
  extends HttpBodyBaseEntry {
  contentKind: HttpContentKind.Html;
  content: string;
}

export interface HttpTextBodyEntry
  extends HttpBodyBaseEntry {
  contentKind: HttpContentKind.Text;
  content: string;
}

export interface HttpImageBodyEntry
  extends HttpBodyBaseEntry {
  contentKind: HttpContentKind.Image;
  binaryContentBase64: string;
}

export interface HttpBinaryBodyEntry
  extends HttpBodyBaseEntry {
  contentKind: HttpContentKind.Binary;
  binaryContentBase64: string;
}

export interface HttpFormUrlEncodedBodyEntry
  extends HttpBodyBaseEntry {
  contentKind: HttpContentKind.FormUrlEncoded;
  content: string;
}

// Unified state for display row mapping
export interface MergedTrafficEntry {
  id: string;
  timestamp: string;
  inbound: string;
  requestURI: string;
  requestMethod: string;
  route: string;
  targetHost: string;
  statusCode: number | null;
  statusText: string;
  durationMs: number | null;
  requestHeaders: Record<string, string>;
  responseHeaders: Record<string, string>;
  requestBody: HttpBodyEntry | null;
  responseBody: HttpBodyEntry | null;
}

export interface HttpHistoryDto {
  requests: HttpRequestEntry[];
  responses: HttpResponseEntry[];
  requestBodies: HttpBodyEntry[];
  responseBodies: HttpBodyEntry[];
}